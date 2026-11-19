namespace EmployeeManagement.Application.DTOs;

/// <summary>
/// A single turn in a conversation — either a user message or an assistant reply.
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>"user" or "assistant"</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
