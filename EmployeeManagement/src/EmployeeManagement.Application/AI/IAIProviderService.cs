namespace EmployeeManagement.Application.AI;

/// <summary>
/// Provider-independent interface for all AI integrations.
/// Implement this interface to add a new AI provider without touching ChatService or business logic.
/// </summary>
public interface IAIProviderService
{
    /// <summary>Human-readable provider name (e.g. "Gemini", "OpenRouter").</summary>
    string ProviderName { get; }

    /// <summary>Model identifier currently in use.</summary>
    string ModelName { get; }

    /// <summary>
    /// Sends a chat message to the AI and returns a structured response.
    /// </summary>
    Task<AIResponse> ProcessChatAsync(AIRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the provider is reachable and the API key is valid.
    /// Returns false instead of throwing so callers can handle gracefully.
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
