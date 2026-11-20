namespace EmployeeManagement.Application.AI;

/// <summary>
/// Discovers and runs tools. The production implementation is an MCP client talking to the
/// application's own MCP server; tests substitute a fake.
/// </summary>
public interface IToolInvoker
{
    Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default);

    /// <param name="name">Tool name.</param>
    /// <param name="argumentsJson">JSON object string with the tool arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ToolResult> CallToolAsync(string name, string argumentsJson, CancellationToken cancellationToken = default);
}

/// <summary>Creates and validates the opaque tokens that confirm a destructive action.</summary>
public interface IPendingActionProtector
{
    /// <summary>Returns a token valid until <paramref name="expiresAtUtc"/>.</summary>
    string Protect(PendingAction action, out DateTime expiresAtUtc);

    /// <summary>Returns the action, or null when the token is invalid, tampered with or expired.</summary>
    PendingAction? Unprotect(string token);
}
