using EmployeeManagement.Application.DTOs;

namespace EmployeeManagement.Application.AI;

/// <summary>Provider-independent chat-completion interface with tool calling.</summary>
public interface IAIProviderService
{
    string ProviderName { get; }
    string ModelName { get; }

    /// <summary>
    /// Sends the transcript and the available tools to the model and returns its next turn.
    /// Throws when the provider cannot be reached or returns an error.
    /// </summary>
    Task<AIChatTurn> CompleteAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinition> tools,
        string? preferredModel,
        CancellationToken cancellationToken = default);
}
