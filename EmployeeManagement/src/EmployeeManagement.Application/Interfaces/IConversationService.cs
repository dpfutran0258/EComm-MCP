using EmployeeManagement.Application.DTOs;

namespace EmployeeManagement.Application.Interfaces;

/// <summary>
/// Thread-safe storage for conversation history keyed by session ID.
/// Implementations may store turns in memory, a database, or a distributed cache.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Returns the most recent <paramref name="maxMessages"/> turns for the session,
    /// ordered oldest-first. Returns an empty list when the session does not exist.
    /// </summary>
    Task<List<ConversationMessage>> GetHistoryAsync(string sessionId, int maxMessages = 20);

    /// <summary>Appends a user or assistant turn to the session history.</summary>
    Task AddMessageAsync(string sessionId, ConversationMessage message);

    /// <summary>Removes all messages for the session.</summary>
    Task ClearHistoryAsync(string sessionId);

    /// <summary>Returns true when any messages exist for the session.</summary>
    Task<bool> SessionExistsAsync(string sessionId);
}
