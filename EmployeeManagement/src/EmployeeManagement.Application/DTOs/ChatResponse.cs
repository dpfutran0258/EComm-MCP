namespace EmployeeManagement.Application.DTOs;

/// <summary>
/// Response returned by POST /api/chat/message.
/// </summary>
public sealed record ChatResponse
{
    /// <summary>AI-generated natural language response shown to the user.</summary>
    public string ResponseText { get; set; } = null!;

    /// <summary>Semantic type of the response — drives frontend rendering.</summary>
    public ChatResponseType Type { get; set; }

    /// <summary>
    /// Structured payload (employee records, bulk results, etc.) for the frontend.
    /// Null when the response is purely conversational.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Updated conversation context returned to the client so it can be echoed
    /// back on the next turn.
    /// </summary>
    public Dictionary<string, object> UpdatedContext { get; set; } = new();

    /// <summary>Name of the AI provider that generated this response.</summary>
    public string? ProviderUsed { get; set; }

    /// <summary>Model identifier used for this response.</summary>
    public string? ModelUsed { get; set; }
}
