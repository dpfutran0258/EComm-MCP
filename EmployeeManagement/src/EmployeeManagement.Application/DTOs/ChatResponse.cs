namespace EmployeeManagement.Application.DTOs;

/// <summary>A destructive action waiting for the user's explicit confirmation.</summary>
public sealed class PendingActionDto
{
    /// <summary>Opaque, tamper-proof, time-limited token. Send it back as <see cref="ChatRequest.ConfirmAction"/>.</summary>
    public string Token { get; set; } = string.Empty;
    public string Tool { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

/// <summary>A tool the assistant used while answering (shown in the UI).</summary>
public sealed class ToolActivityDto
{
    public string Tool { get; set; } = string.Empty;
    public bool IsError { get; set; }
}

/// <summary>Response returned by POST /api/chat.</summary>
public sealed record ChatResponse
{
    /// <summary>Text shown to the user.</summary>
    public string ResponseText { get; set; } = string.Empty;

    /// <summary>Semantic type of the response; drives frontend styling.</summary>
    public ChatResponseType Type { get; set; }

    /// <summary>Messages the client must append to its transcript (assistant tool calls, tool results, final text).</summary>
    public List<ChatMessageDto> NewMessages { get; set; } = new();

    /// <summary>Tools used for this turn, in order.</summary>
    public List<ToolActivityDto> ToolActivity { get; set; } = new();

    /// <summary>Structured payload of the last successful tool call (employee, employees, results, stats), for UI cards.</summary>
    public object? Data { get; set; }

    /// <summary>True when a tool changed employee data, so the UI should refresh its lists.</summary>
    public bool DataChanged { get; set; }

    /// <summary>Set when a destructive action needs the user's confirmation.</summary>
    public PendingActionDto? PendingAction { get; set; }

    public string? ProviderUsed { get; set; }
    public string? ModelUsed { get; set; }
}
