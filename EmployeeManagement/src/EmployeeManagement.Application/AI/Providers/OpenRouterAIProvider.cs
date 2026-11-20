using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EmployeeManagement.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace EmployeeManagement.Application.AI.Providers;

/// <summary>
/// OpenRouter AI provider adapter (supports NVIDIA Nemotron, GPT-4o-mini, and all OpenRouter models).
/// Implements the OpenAI-compatible chat completions API.
/// </summary>
public sealed class OpenRouterAIProvider : IAIProviderService
{
    private const int MaxRetries = 3;

    private readonly OpenRouterProviderOptions _options;
    private readonly ILogger<OpenRouterAIProvider> _logger;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ProviderName => "OpenRouter";
    public string ModelName => _options.Model;

    public OpenRouterAIProvider(OpenRouterProviderOptions options, ILogger<OpenRouterAIProvider> logger, HttpClient httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public string ProviderDisplayName => $"{ProviderName} ({_options.Model})";

    public async Task<AIResponse> ProcessChatAsync(AIRequest request, CancellationToken cancellationToken = default)
    {
        var start = DateTime.UtcNow;
        // Only honour a client-requested model when it is explicitly allow-listed.
        var model = _options.IsModelAllowed(request.PreferredModel) ? request.PreferredModel! : _options.Model;
        var retryCounter = new RetryCounter();

        try
        {
            _logger.LogInformation(
                "OpenRouter request starting. Session={SessionId}, Model={Model}, HistoryCount={Count}",
                request.SessionId, model, request.History.Count);

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("User message: {Message}", request.UserMessage);
                _logger.LogDebug("Employee context count: {Count}", request.EmployeeContext.Count);
            }

            var messages = BuildMessages(request);
            var rawJson = await CallWithRetriesAsync(messages, model, retryCounter, cancellationToken);
            
            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("Raw AI response: {Response}", rawJson);
            }
            
            var aiResponse = ParseResponse(rawJson);

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "Extracted entities: FullName={FullName}, Email={Email}, EmployeeId={EmployeeId}, Department={Department}, Salary={Salary}",
                    aiResponse.Entities.FullName,
                    aiResponse.Entities.Email,
                    aiResponse.Entities.EmployeeId,
                    aiResponse.Entities.Department,
                    aiResponse.Entities.Salary);
            }

            var duration = DateTime.UtcNow - start;
            _logger.LogInformation(
                "OpenRouter response received. Session={SessionId}, Intent={Intent}, Confidence={Confidence}, Duration={Duration}ms, Retries={Retries}",
                request.SessionId, aiResponse.Intent, aiResponse.Confidence, (int)duration.TotalMilliseconds, retryCounter.Count);

            return aiResponse with
            {
                Metadata = aiResponse.Metadata with
                {
                    Provider = ProviderName,
                    Model = model,
                    Duration = duration,
                    RetryCount = retryCounter.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouter provider failed. Session={SessionId}", request.SessionId);
            return AIResponse.Fallback(ProviderName, ex.Message) with
            {
                Metadata = new AIResponseMetadata
                {
                    Provider = ProviderName,
                    Model = model,
                    Duration = DateTime.UtcNow - start,
                    RetryCount = retryCounter.Count,
                    Error = ex.Message
                }
            };
        }
    }

    private List<object> BuildMessages(AIRequest request)
    {
        var systemPrompt = AISystemPromptBuilder.Build(request.EmployeeContext);
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        // Inject conversation history
        if (_options.EnableDetailedLogging && request.History.Count > 0)
        {
            _logger.LogDebug("Including {Count} conversation history messages:", request.History.Count);
            foreach (var msg in request.History)
            {
                _logger.LogDebug("  [{Role}] {Content}", msg.Role, 
                    msg.Content.Length > 100 ? msg.Content.Substring(0, 100) + "..." : msg.Content);
            }
        }

        foreach (var msg in request.History)
            messages.Add(new { role = msg.Role, content = msg.Content });

        messages.Add(new { role = "user", content = request.UserMessage });
        
        if (_options.EnableDetailedLogging)
        {
            _logger.LogDebug("Total messages being sent to AI: {Count} (1 system + {History} history + 1 user)", 
                messages.Count, request.History.Count);
        }
        
        return messages;
    }

    private async Task<string> CallWithRetriesAsync(
        List<object> messages,
        string model,
        RetryCounter retryCounter,
        CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";
        var requestBody = new
        {
            model,
            messages,
            temperature = _options.Temperature,
            top_p = _options.TopP,
            max_tokens = _options.MaxOutputTokens
        };
        var json = JsonSerializer.Serialize(requestBody);

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
                retryCounter.Count++;
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
                    retryCounter.Count++;
                    _logger.LogWarning("OpenRouter returned {Status}, retrying {Attempt}/{Max}", (int)response.StatusCode, attempt + 1, MaxRetries);
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                    continue;
                }

                throw new HttpRequestException($"OpenRouter API returned {(int)response.StatusCode}: {body}");
            }
        }

        throw new HttpRequestException("OpenRouter API failed after all retries.");
    }

    private AIResponse ParseResponse(string apiResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(apiResponse);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                throw new InvalidOperationException("OpenRouter returned no choices.");

            var content = choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            // Aggressive cleanup for malformed responses
            content = content.Trim();
            
            // Remove markdown fences
            if (content.StartsWith("```"))
            {
                content = content.Replace("```json", "").Replace("```", "").Trim();
            }
            
            // Some models wrap JSON in extra text — extract JSON if present
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                content = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            return ParseAIJson(content, root);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenRouter response.");
            return AIResponse.Fallback(ProviderName, ex.Message);
        }
    }

    private static AIResponse ParseAIJson(string json, JsonElement rootDoc)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var entities = new ExtractedEntities();
        if (root.TryGetProperty("entities", out var e))
        {
            entities.EmployeeId = TryGetInt(e, "employeeId");
            entities.FullName = TryGetString(e, "fullName");
            entities.Email = TryGetString(e, "email");
            entities.Department = TryGetString(e, "department");
            entities.Salary = TryGetDecimal(e, "salary");
            entities.SearchQuery = TryGetString(e, "searchQuery");
            entities.BulkEmployees = TryGetBulkEmployees(e);
            entities.BulkOperation = TryGetBulkOperation(e);
        }

        var suggestions = new List<string>();
        if (root.TryGetProperty("suggestions", out var sugg) && sugg.ValueKind == JsonValueKind.Array)
            suggestions = sugg.EnumerateArray()
                .Select(s => s.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

        var intentStr = root.TryGetProperty("intent", out var i) ? i.GetString() : "UNKNOWN";
        if (!Enum.TryParse<ChatIntent>(intentStr, ignoreCase: true, out var intent))
            intent = ChatIntent.UNKNOWN;

        var confidence = root.TryGetProperty("confidence", out var c) ? (decimal)c.GetDouble() : 0m;
        var responseText = root.TryGetProperty("responseText", out var r)
            ? r.GetString() ?? "I'm not sure how to respond to that."
            : "I'm not sure how to respond to that.";

        // Extract token usage if available
        int? inputTokens = null, outputTokens = null;
        if (rootDoc.TryGetProperty("usage", out var usage))
        {
            inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : null;
            outputTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : null;
        }

        return new AIResponse
        {
            ResponseText = responseText,
            Intent = intent,
            Confidence = Math.Clamp(confidence, 0m, 1m),
            Entities = entities,
            Suggestions = suggestions,
            Metadata = new AIResponseMetadata
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            }
        };
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var req = new AIRequest
            {
                UserMessage = "Hello",
                SessionId = "health-check",
                EmployeeContext = new List<EmployeeContextItem>()
            };
            var result = await ProcessChatAsync(req, cancellationToken);
            return !string.IsNullOrEmpty(result.ResponseText);
        }
        catch
        {
            return false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class RetryCounter { public int Count { get; set; } }

    private static bool IsRetryableStatus(System.Net.HttpStatusCode code) =>
        code == System.Net.HttpStatusCode.TooManyRequests ||
        code == System.Net.HttpStatusCode.ServiceUnavailable ||
        code == System.Net.HttpStatusCode.BadGateway ||
        code == System.Net.HttpStatusCode.GatewayTimeout ||
        code == System.Net.HttpStatusCode.InternalServerError;

    private static TimeSpan GetRetryDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Pow(2, attempt) + Random.Shared.NextDouble());

    private static string? TryGetString(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? TryGetInt(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static decimal? TryGetDecimal(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? (decimal)v.GetDouble() : null;

    private static List<BulkEmployeeData>? TryGetBulkEmployees(JsonElement entities)
    {
        if (!entities.TryGetProperty("bulkEmployees", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<BulkEmployeeData>();
        foreach (var item in arr.EnumerateArray())
        {
            list.Add(new BulkEmployeeData
            {
                FullName = TryGetString(item, "fullName") ?? TryGetString(item, "name"),
                Email = TryGetString(item, "email"),
                Department = TryGetString(item, "department"),
                Salary = TryGetDecimal(item, "salary")
            });
        }
        return list.Count > 0 ? list : null;
    }

    private static BulkOperationType? TryGetBulkOperation(JsonElement entities)
    {
        var val = TryGetString(entities, "bulkOperation");
        if (val is null) return null;
        return Enum.TryParse<BulkOperationType>(val, ignoreCase: true, out var r) ? r : null;
    }
}
