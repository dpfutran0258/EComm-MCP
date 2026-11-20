using System.Text.Json;
using EmployeeManagement.Application.DTOs;

namespace EmployeeManagement.Application.AI;

/// <summary>A tool the model may call, as advertised by the MCP server.</summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonElement ParametersSchema,
    bool IsReadOnly,
    bool IsDestructive);

/// <summary>Result of executing a tool.</summary>
public sealed record ToolResult(string Text, bool IsError);

/// <summary>One model completion: text and/or tool calls.</summary>
public sealed record AIChatTurn(
    string? Text,
    IReadOnlyList<ChatToolCallDto> ToolCalls,
    string Provider,
    string Model);

/// <summary>A destructive tool call held until the user confirms it.</summary>
public sealed record PendingAction(string Tool, string ArgumentsJson, string Summary);
