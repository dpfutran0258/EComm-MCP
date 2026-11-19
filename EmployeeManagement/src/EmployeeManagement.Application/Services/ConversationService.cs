using System.Collections.Concurrent;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EmployeeManagement.Application.Services;

/// <summary>
/// Thread-safe in-memory conversation history store.
/// Replaces the non-thread-safe <see cref="Dictionary{TKey,TValue}"/> that
/// was scattered across GeminiService and OpenRouterService.
/// A <see cref="ConcurrentDictionary"/> provides safe concurrent access,
/// and history is capped at <see cref="MaxMessagesPerSession"/> to prevent unbounded growth.
/// </summary>
public sealed class ConversationService : IConversationService
{
    private const int MaxMessagesPerSession = 40; // 20 turns

    private readonly ConcurrentDictionary<string, List<ConversationMessage>> _sessions = new();
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(ILogger<ConversationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<ConversationMessage>> GetHistoryAsync(string sessionId, int maxMessages = 20)
    {
        if (!_sessions.TryGetValue(sessionId, out var history))
            return Task.FromResult(new List<ConversationMessage>());

        lock (history)
        {
            var slice = history.TakeLast(Math.Min(maxMessages, history.Count)).ToList();
            return Task.FromResult(slice);
        }
    }

    /// <inheritdoc />
    public Task AddMessageAsync(string sessionId, ConversationMessage message)
    {
        var history = _sessions.GetOrAdd(sessionId, _ => new List<ConversationMessage>());

        lock (history)
        {
            history.Add(message);

            // Trim to cap — remove oldest messages first
            if (history.Count > MaxMessagesPerSession)
            {
                var excess = history.Count - MaxMessagesPerSession;
                history.RemoveRange(0, excess);
                _logger.LogDebug("Trimmed {Excess} old messages from session {SessionId}", excess, sessionId);
            }
            
            _logger.LogDebug("Added {Role} message to session {SessionId}. Total messages: {Count}", 
                message.Role, sessionId, history.Count);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearHistoryAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out _))
            _logger.LogInformation("Conversation history cleared for session {SessionId}", sessionId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> SessionExistsAsync(string sessionId) =>
        Task.FromResult(_sessions.ContainsKey(sessionId));
}
