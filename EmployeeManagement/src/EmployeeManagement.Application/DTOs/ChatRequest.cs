namespace EmployeeManagement.Application.DTOs;

/// <summary>
/// Incoming request body for the POST /api/chat/message endpoint.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>The user's message. Required, non-empty.</summary>
    public string Message { get; set; } = null!;

    /// <summary>Stable session identifier. Required.</summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// Client-side conversation context (pending entities, confirmations, etc.).
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Optional model override. Must be one of the server's allowed models
    /// (AI:OpenRouter:AllowedModels or the configured Model); otherwise the request is rejected with 400.
    /// </summary>
    public string? PreferredModel { get; set; }
}
