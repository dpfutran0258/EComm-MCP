namespace EmployeeManagement.Application.DTOs;

/// <summary>One tool invocation requested by the model.</summary>
public sealed class ChatToolCallDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Arguments as a JSON object string.</summary>
    public string Arguments { get; set; } = "{}";
}

/// <summary>
/// One message of the conversation transcript. The client keeps the transcript and sends it
/// with every request, so the server stays stateless.
/// <list type="bullet">
/// <item><c>user</c>: text typed by the user.</item>
/// <item><c>assistant</c>: text and/or <see cref="ToolCalls"/> produced by the model.</item>
/// <item><c>tool</c>: the result of a tool call, linked by <see cref="ToolCallId"/>.</item>
/// </list>
/// </summary>
public sealed class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string? Content { get; set; }
    public List<ChatToolCallDto>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
}

/// <summary>Incoming body for POST /api/chat.</summary>
public sealed class ChatRequest
{
    /// <summary>The conversation so far, ending with the new user message. Required unless <see cref="ConfirmAction"/> is set.</summary>
    public List<ChatMessageDto> Messages { get; set; } = new();

    /// <summary>
    /// Token from a previous <see cref="ChatResponse.PendingAction"/>. When present the server runs the
    /// confirmed action directly (no model call).
    /// </summary>
    public string? ConfirmAction { get; set; }

    /// <summary>Optional model override; must be in AI:OpenRouter:AllowedModels or equal the configured model.</summary>
    public string? PreferredModel { get; set; }
}
