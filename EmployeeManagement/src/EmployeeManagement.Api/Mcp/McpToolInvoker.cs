using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using EmployeeManagement.Application.AI;

namespace EmployeeManagement.Api.Mcp;

/// <summary>Settings for the chat host's MCP client.</summary>
public sealed class McpClientOptions
{
    public const string SectionName = "Mcp";

    /// <summary>URL of the application's own MCP endpoint (see <c>MapMcp</c> in Program.cs).</summary>
    public string Endpoint { get; set; } = "http://localhost:5000/mcp";
}

/// <summary>
/// <see cref="IToolInvoker"/> backed by a real MCP client. Each call opens a short-lived connection to the
/// MCP server (stateless Streamable HTTP), so nothing is shared between chat requests.
/// </summary>
public sealed class McpToolInvoker : IToolInvoker
{
    /// <summary>Name of the <see cref="HttpClient"/> registered for the MCP loopback connection.</summary>
    public const string HttpClientName = "McpLoopback";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly McpClientOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public McpToolInvoker(IHttpClientFactory httpClientFactory, McpClientOptions options, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _loggerFactory = loggerFactory;
    }

    public async Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await using var client = await ConnectAsync(cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

        return tools
            .Select(t => new ToolDefinition(
                t.Name,
                t.Description ?? string.Empty,
                t.JsonSchema,
                IsReadOnly: t.ProtocolTool.Annotations?.ReadOnlyHint == true,
                IsDestructive: t.ProtocolTool.Annotations?.DestructiveHint == true))
            .ToList();
    }

    public async Task<ToolResult> CallToolAsync(string name, string argumentsJson, CancellationToken cancellationToken = default)
    {
        var arguments = ParseArguments(argumentsJson);

        await using var client = await ConnectAsync(cancellationToken);
        var result = await client.CallToolAsync(name, arguments, cancellationToken: cancellationToken);

        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        return new ToolResult(text, result.IsError == true);
    }

    private async Task<McpClient> ConnectAsync(CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_options.Endpoint),
                TransportMode = HttpTransportMode.StreamableHttp,
                Name = "employee-management-chat-host"
            },
            _httpClientFactory.CreateClient(HttpClientName),
            _loggerFactory,
            ownsHttpClient: false);

        return await McpClient.CreateAsync(transport, loggerFactory: _loggerFactory, cancellationToken: cancellationToken);
    }

    private static Dictionary<string, object?> ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return new Dictionary<string, object?>();

        using var doc = JsonDocument.Parse(argumentsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Tool arguments must be a JSON object.");

        return doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
    }
}
