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
    /// Optional AI provider override (e.g. "Gemini", "OpenRouter").
    /// Falls back to the configured default when null.
    /// </summary>
    public string? PreferredProvider { get; set; }

    /// <summary>Optional model override (e.g. "nvidia/nemotron-3-ultra-550b-a55b:free").</summary>
    public string? PreferredModel { get; set; }
}
