using System.Text.Json;
using EmployeeManagement.Application.AI;
using EmployeeManagement.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace EmployeeManagement.Application.Services;

/// <summary>
/// Chat orchestrator (MCP host). The user's raw message and the transcript sent by the client go to the
/// model together with the tools exposed by the MCP server. The model decides which tool to call; this
/// class runs the tool calls and feeds the results back until the model produces a final answer.
///
/// Destructive tools (MCP annotation <c>destructive</c>) are never run on the model's say-so: the user gets a
/// confirmation prompt with a signed, time-limited token, and the confirmed call runs without the model.
///
/// The service is stateless. All conversation context comes from <see cref="ChatRequest.Messages"/>.
/// </summary>
public sealed class ChatAgentService
{
    /// <summary>Maximum model round trips for one user message.</summary>
    public const int MaxIterations = 6;

    /// <summary>Maximum messages kept from the client transcript.</summary>
    public const int MaxMessages = 40;

    /// <summary>Maximum total characters of the transcript sent to the model.</summary>
    public const int MaxTranscriptChars = 60_000;

    /// <summary>Maximum characters of a single tool result sent to the model.</summary>
    public const int MaxToolResultChars = 12_000;

    private const int MaxDescribedTargets = 10;

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal) { "user", "assistant", "tool" };

    private readonly IAIProviderService _ai;
    private readonly IToolInvoker _tools;
    private readonly IPendingActionProtector _protector;
    private readonly ILogger<ChatAgentService> _logger;

    public ChatAgentService(
        IAIProviderService ai,
        IToolInvoker tools,
        IPendingActionProtector protector,
        ILogger<ChatAgentService> logger)
    {
        _ai = ai;
        _tools = tools;
        _protector = protector;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.ConfirmAction))
            return await ExecuteConfirmedAsync(request.ConfirmAction, cancellationToken);

        var transcript = PrepareTranscript(request.Messages);
        if (transcript.Count == 0 || transcript[^1].Role != "user" || string.IsNullOrWhiteSpace(transcript[^1].Content))
            throw new ArgumentException("The last message must be a non-empty user message.");

        IReadOnlyList<ToolDefinition> tools;
        try
        {
            tools = await _tools.ListToolsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not load tools from the MCP server.");
            return ErrorResponse("I can't reach the employee tools right now. Please try again in a moment.");
        }

        var toolsByName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var working = new List<ChatMessageDto>(transcript);
        var newMessages = new List<ChatMessageDto>();
        var activity = new List<ToolActivityDto>();
        object? data = null;
        var dataChanged = false;
        string? provider = null, model = null;

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            AIChatTurn turn;
            try
            {
                turn = await _ai.CompleteAsync(working, tools, request.PreferredModel, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException
                                           || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                _logger.LogError(ex, "AI provider failed.");
                return ErrorResponse("The AI service didn't respond properly. Please try again.", newMessages, activity, dataChanged);
            }

            provider = turn.Provider;
            model = turn.Model;

            // No tool calls → final answer.
            if (turn.ToolCalls.Count == 0)
            {
                var text = string.IsNullOrWhiteSpace(turn.Text) ? "Done." : turn.Text.Trim();
                newMessages.Add(new ChatMessageDto { Role = "assistant", Content = text });

                return new ChatResponse
                {
                    ResponseText = text,
                    Type = ChooseType(activity, text),
                    NewMessages = newMessages,
                    ToolActivity = activity,
                    Data = data,
                    DataChanged = dataChanged,
                    ProviderUsed = provider,
                    ModelUsed = model
                };
            }

            // A destructive call stops the loop: ask the user, never run it here.
            var destructive = turn.ToolCalls.FirstOrDefault(c => toolsByName.TryGetValue(c.Name, out var d) && d.IsDestructive);
            if (destructive is not null)
            {
                return await BuildConfirmationAsync(destructive, newMessages, activity, dataChanged, provider, model, cancellationToken);
            }

            var assistantMessage = new ChatMessageDto
            {
                Role = "assistant",
                Content = turn.Text,
                ToolCalls = turn.ToolCalls.ToList()
            };
            working.Add(assistantMessage);
            newMessages.Add(assistantMessage);

            foreach (var call in turn.ToolCalls)
            {
                var result = await RunToolAsync(call, toolsByName, cancellationToken);
                activity.Add(new ToolActivityDto { Tool = call.Name, IsError = result.IsError });

                var toolMessage = new ChatMessageDto
                {
                    Role = "tool",
                    ToolCallId = call.Id,
                    Content = Truncate(result.IsError ? "ERROR: " + result.Text : result.Text, MaxToolResultChars)
                };
                working.Add(toolMessage);
                newMessages.Add(toolMessage);

                if (!result.IsError)
                {
                    if (TryParseJson(result.Text) is { } parsed) data = parsed;
                    if (toolsByName.TryGetValue(call.Name, out var def) && !def.IsReadOnly) dataChanged = true;
                }
            }
        }

        _logger.LogWarning("Chat agent hit the iteration limit ({Max}).", MaxIterations);
        return ErrorResponse(
            "I couldn't finish that request. Please try rephrasing or break it into smaller steps.",
            newMessages, activity, dataChanged);
    }

    // ── Confirmation of destructive actions ──────────────────────────────────

    private async Task<ChatResponse> BuildConfirmationAsync(
        ChatToolCallDto call,
        List<ChatMessageDto> newMessages,
        List<ToolActivityDto> activity,
        bool dataChanged,
        string? provider,
        string? model,
        CancellationToken ct)
    {
        var summary = await DescribeAsync(call, ct);
        var token = _protector.Protect(new PendingAction(call.Name, call.Arguments, summary), out var expiresAt);

        var text = $"{summary}\n\nThis can't be undone. Please confirm.";
        newMessages.Add(new ChatMessageDto { Role = "assistant", Content = text });

        return new ChatResponse
        {
            ResponseText = text,
            Type = ChatResponseType.CONFIRMATION,
            NewMessages = newMessages,
            ToolActivity = activity,
            DataChanged = dataChanged,
            PendingAction = new PendingActionDto
            {
                Token = token,
                Tool = call.Name,
                Summary = summary,
                ExpiresAtUtc = expiresAt
            },
            ProviderUsed = provider,
            ModelUsed = model
        };
    }

    private async Task<ChatResponse> ExecuteConfirmedAsync(string token, CancellationToken ct)
    {
        var action = _protector.Unprotect(token);
        if (action is null)
            return ErrorResponse("That confirmation is invalid or has expired. Please ask me again.");

        var result = await _tools.CallToolAsync(action.Tool, action.ArgumentsJson, ct);

        string text;
        if (result.IsError)
        {
            text = $"❌ {result.Text}";
        }
        else
        {
            text = ReadMessage(result.Text) ?? "✅ Done.";
        }

        var response = new ChatResponse
        {
            ResponseText = text,
            Type = result.IsError ? ChatResponseType.ERROR : ChatResponseType.SUCCESS,
            NewMessages = new List<ChatMessageDto> { new() { Role = "assistant", Content = text } },
            ToolActivity = new List<ToolActivityDto> { new() { Tool = action.Tool, IsError = result.IsError } },
            DataChanged = !result.IsError,
            Data = result.IsError ? null : TryParseJson(result.Text)
        };

        _logger.LogInformation("Confirmed action executed. Tool={Tool}, Error={IsError}", action.Tool, result.IsError);
        return response;
    }

    /// <summary>Builds a human-readable description of a destructive call, resolving ids to names via read-only tools.</summary>
    private async Task<string> DescribeAsync(ChatToolCallDto call, CancellationToken ct)
    {
        try
        {
            using var args = JsonDocument.Parse(call.Arguments);
            var root = args.RootElement;

            if (call.Name == "delete_employee" && root.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var id))
            {
                var line = await DescribeEmployeeAsync(id, ct);
                return $"⚠️ Delete {line}?";
            }

            if (call.Name == "bulk_delete_employees" && root.TryGetProperty("ids", out var idsEl) && idsEl.ValueKind == JsonValueKind.Array)
            {
                var ids = idsEl.EnumerateArray().Where(e => e.TryGetInt32(out _)).Select(e => e.GetInt32()).Distinct().ToList();
                var lines = new List<string>();
                foreach (var empId in ids.Take(MaxDescribedTargets))
                    lines.Add("• " + await DescribeEmployeeAsync(empId, ct));
                if (ids.Count > MaxDescribedTargets)
                    lines.Add($"• … and {ids.Count - MaxDescribedTargets} more");

                return $"⚠️ Delete {ids.Count} employee{(ids.Count == 1 ? "" : "s")}?\n" + string.Join("\n", lines);
            }
        }
        catch (JsonException)
        {
            // fall through to the generic description
        }

        return $"⚠️ Run '{call.Name}'?";
    }

    private async Task<string> DescribeEmployeeAsync(int id, CancellationToken ct)
    {
        var result = await _tools.CallToolAsync("get_employee", JsonSerializer.Serialize(new { id }), ct);
        if (!result.IsError && TryParseJson(result.Text) is JsonElement root
            && root.TryGetProperty("employee", out var e)
            && e.TryGetProperty("fullName", out var name))
        {
            var email = e.TryGetProperty("email", out var em) ? em.GetString() : null;
            return email is null ? $"{name.GetString()} (ID {id})" : $"{name.GetString()} ({email}, ID {id})";
        }

        return $"employee with ID {id}";
    }

    // ── Tool execution ───────────────────────────────────────────────────────

    private async Task<ToolResult> RunToolAsync(
        ChatToolCallDto call, IReadOnlyDictionary<string, ToolDefinition> toolsByName, CancellationToken ct)
    {
        if (!toolsByName.ContainsKey(call.Name))
            return new ToolResult($"Unknown tool '{call.Name}'.", true);

        try
        {
            using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.Arguments) ? "{}" : call.Arguments);
        }
        catch (JsonException)
        {
            return new ToolResult("The tool arguments were not valid JSON.", true);
        }

        try
        {
            var result = await _tools.CallToolAsync(call.Name, call.Arguments, ct);
            _logger.LogInformation("Tool executed. Tool={Tool}, Error={IsError}", call.Name, result.IsError);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Tool call failed. Tool={Tool}", call.Name);
            return new ToolResult("The tool failed unexpectedly.", true);
        }
    }

    // ── Transcript handling ──────────────────────────────────────────────────

    /// <summary>
    /// Keeps only known roles, caps the transcript by message count and size, and makes sure it starts with a user
    /// message so no tool result is left without the assistant tool call that produced it.
    /// </summary>
    public static List<ChatMessageDto> PrepareTranscript(IEnumerable<ChatMessageDto>? messages)
    {
        var list = (messages ?? Enumerable.Empty<ChatMessageDto>())
            .Where(m => m is not null && AllowedRoles.Contains(m.Role))
            .Select(m => new ChatMessageDto
            {
                Role = m.Role,
                Content = m.Role == "tool" ? Truncate(m.Content, MaxToolResultChars) : m.Content,
                ToolCalls = m.ToolCalls,
                ToolCallId = m.ToolCallId
            })
            .ToList();

        if (list.Count > MaxMessages)
            list = list.GetRange(list.Count - MaxMessages, MaxMessages);

        // Drop the oldest messages while the transcript is too large (always keeping the last message).
        while (list.Count > 1 && list.Sum(Size) > MaxTranscriptChars)
            list.RemoveAt(0);

        // The transcript must start with a user message.
        var firstUser = list.FindIndex(m => m.Role == "user");
        if (firstUser > 0) list.RemoveRange(0, firstUser);
        else if (firstUser < 0) list.Clear();

        return list;

        static int Size(ChatMessageDto m) =>
            (m.Content?.Length ?? 0) + (m.ToolCalls?.Sum(c => c.Arguments.Length + c.Name.Length) ?? 0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ChatResponseType ChooseType(List<ToolActivityDto> activity, string text)
    {
        if (activity.Count == 0)
            return text.TrimEnd().EndsWith('?') ? ChatResponseType.CLARIFICATION : ChatResponseType.INFO;
        return activity.All(a => a.IsError) ? ChatResponseType.ERROR : ChatResponseType.SUCCESS;
    }

    private static ChatResponse ErrorResponse(
        string text,
        List<ChatMessageDto>? newMessages = null,
        List<ToolActivityDto>? activity = null,
        bool dataChanged = false) =>
        new()
        {
            ResponseText = text,
            Type = ChatResponseType.ERROR,
            // Keep completed tool steps so the transcript stays consistent, but add no dangling assistant text.
            NewMessages = newMessages ?? new List<ChatMessageDto>(),
            ToolActivity = activity ?? new List<ToolActivityDto>(),
            DataChanged = dataChanged
        };

    private static JsonElement? TryParseJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadMessage(string json) =>
        TryParseJson(json) is JsonElement root && root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()
            : null;

    private static string? Truncate(string? text, int max) =>
        text is null || text.Length <= max ? text : text[..max] + "…[truncated]";
}
