using EmployeeManagement.Application.DTOs;

namespace EmployeeManagement.Application.AI;

/// <summary>
/// Provider-independent chat request contract.
/// All AI providers receive this same input.
/// </summary>
public sealed class AIRequest
{
    /// <summary>The user's raw message.</summary>
    public string UserMessage { get; init; } = string.Empty;

    /// <summary>Stable identifier for the user's session. Used to scope conversation history.</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Recent conversation turns to give the model context.</summary>
    public List<ConversationMessage> History { get; init; } = new();

    /// <summary>Subset of employee records relevant to the current request.</summary>
    public List<EmployeeContextItem> EmployeeContext { get; init; } = new();

    /// <summary>
    /// Optional model override. Honoured only when listed in the provider's allowed models;
    /// otherwise the configured model is used.
    /// </summary>

    public string? PreferredModel { get; init; }

    /// <summary>Arbitrary key-value metadata passed through to the provider.</summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Lightweight employee record sent to the AI as context.
/// Only contains fields the AI needs to reason about — no salary unless relevant.
/// </summary>
public sealed class EmployeeContextItem
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public decimal? Salary { get; init; }
}
