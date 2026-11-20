using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EmployeeManagement.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace EmployeeManagement.Application.AI.Providers;

/// <summary>
/// OpenRouter adapter using the OpenAI-compatible chat completions API with native tool calling.
/// The system prompt and the transcript are sent as messages; the available MCP tools are sent as
/// <c>tools</c>, and the model answers with text or <c>tool_calls</c>.
/// </summary>
public sealed class OpenRouterAIProvider : IAIProviderService
{
    private const int MaxRetries = 3;

    private readonly OpenRouterProviderOptions _options;
    private readonly ILogger<OpenRouterAIProvider> _logger;
    private readonly HttpClient _httpClient;

    public string ProviderName => "OpenRouter";
    public string ModelName => _options.Model;

    public OpenRouterAIProvider(OpenRouterProviderOptions options, ILogger<OpenRouterAIProvider> logger, HttpClient httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<AIChatTurn> CompleteAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinition> tools,
        string? preferredModel,
        CancellationToken cancellationToken = default)
    {
        // Only honour a client-requested model when it is explicitly allow-listed.
        var model = _options.IsModelAllowed(preferredModel) ? preferredModel! : _options.Model;
        var started = DateTime.UtcNow;

        var body = BuildRequestBody(messages, tools, model);
        var raw = await CallWithRetriesAsync(body, cancellationToken);
        var turn = ParseResponse(raw, model);

        _logger.LogInformation(
            "OpenRouter completion. Model={Model}, ToolCalls={ToolCalls}, HasText={HasText}, Duration={Duration}ms",
            model, turn.ToolCalls.Count, !string.IsNullOrWhiteSpace(turn.Text),
            (int)(DateTime.UtcNow - started).TotalMilliseconds);

        return turn;
    }

    // ── Request ──────────────────────────────────────────────────────────────

    private string BuildRequestBody(IReadOnlyList<ChatMessageDto> messages, IReadOnlyList<ToolDefinition> tools, string model)
    {
        var wireMessages = new List<object>
        {
            new { role = "system", content = ChatSystemPrompt.Text }
        };

        foreach (var m in messages)
        {
            switch (m.Role)
            {
                case "assistant" when m.ToolCalls is { Count: > 0 }:
                    wireMessages.Add(new
                    {
                        role = "assistant",
                        content = string.IsNullOrWhiteSpace(m.Content) ? null : m.Content,
                        tool_calls = m.ToolCalls.Select(tc => new
                        {
                            id = tc.Id,
                            type = "function",
                            function = new { name = tc.Name, arguments = tc.Arguments }
                        }).ToList()
                    });
                    break;
                case "assistant":
                    wireMessages.Add(new { role = "assistant", content = m.Content ?? string.Empty });
                    break;
                case "tool":
                    wireMessages.Add(new { role = "tool", tool_call_id = m.ToolCallId ?? string.Empty, content = m.Content ?? string.Empty });
                    break;
                default:
                    wireMessages.Add(new { role = "user", content = m.Content ?? string.Empty });
                    break;
            }
        }

        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = wireMessages,
            ["temperature"] = _options.Temperature,
            ["top_p"] = _options.TopP,
            ["max_tokens"] = _options.MaxOutputTokens
        };

        if (tools.Count > 0)
        {
            body["tools"] = tools.Select(t => new
            {
                type = "function",
                function = new { name = t.Name, description = t.Description, parameters = t.ParametersSchema }
            }).ToList();
            body["tool_choice"] = "auto";
        }

        return JsonSerializer.Serialize(body);
    }

    private async Task<string> CallWithRetriesAsync(string json, CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(req, cancellationToken);
            }
            catch (Exception ex) when (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "OpenRouter network error on attempt {Attempt}/{Max}", attempt, MaxRetries);
                await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                continue;
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsSuccessStatusCode)
                    return body;

                if (IsRetryableStatus(response.StatusCode) && attempt < MaxRetries)
                {
                    _logger.LogWarning("OpenRouter returned {Status}, retrying {Attempt}/{Max}", (int)response.StatusCode, attempt + 1, MaxRetries);
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                    continue;
                }

                throw new HttpRequestException($"OpenRouter API returned {(int)response.StatusCode}: {body}");
            }
        }

        throw new HttpRequestException("OpenRouter API failed after all retries.");
    }

    // ── Response ─────────────────────────────────────────────────────────────

    private AIChatTurn ParseResponse(string apiResponse, string model)
    {
        using var doc = JsonDocument.Parse(apiResponse);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            throw new InvalidOperationException("OpenRouter returned no choices.");

        var message = choices[0].GetProperty("message");

        string? text = message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
            ? content.GetString()
            : null;

        var toolCalls = new List<ChatToolCallDto>();
        if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in calls.EnumerateArray())
            {
                if (!call.TryGetProperty("function", out var fn)) continue;
                var name = fn.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var arguments = fn.TryGetProperty("arguments", out var a)
                    ? a.ValueKind == JsonValueKind.String ? a.GetString() : a.GetRawText()
                    : null;

                toolCalls.Add(new ChatToolCallDto
                {
                    Id = call.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } callId
                        ? callId
                        : $"call-{Guid.NewGuid():N}",
                    Name = name,
                    Arguments = string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments
                });
            }
        }

        return new AIChatTurn(text, toolCalls, ProviderName, model);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsRetryableStatus(System.Net.HttpStatusCode code) =>
        code == System.Net.HttpStatusCode.TooManyRequests ||
        code == System.Net.HttpStatusCode.ServiceUnavailable ||
        code == System.Net.HttpStatusCode.BadGateway ||
        code == System.Net.HttpStatusCode.GatewayTimeout ||
        code == System.Net.HttpStatusCode.InternalServerError;

    private static TimeSpan GetRetryDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Pow(2, attempt) + Random.Shared.NextDouble());
}
