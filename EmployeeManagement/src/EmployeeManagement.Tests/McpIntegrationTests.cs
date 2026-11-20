using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Client;
using EmployeeManagement.Api.Mcp;
using EmployeeManagement.Application.AI;
using EmployeeManagement.Application.Interfaces;

namespace EmployeeManagement.Tests;

/// <summary>
/// Hosts the real API in memory (real MCP server, real MCP client, real chat endpoint and JSON pipeline) with an
/// in-memory repository and a scripted model, so no database or LLM is involved.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public InMemoryEmployeeRepository Repository { get; } = new();
    public ScriptedAIProvider Ai { get; } = new();

    /// <summary>When false the real OpenRouter provider is used (see LiveLlmSmokeTests).</summary>
    protected virtual bool UseScriptedAi => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused;Database=unused");
        builder.UseSetting("AI:OpenRouter:ApiKey", UseScriptedAi ? "test-key" : Environment.GetEnvironmentVariable("EM_OPENROUTER_KEY") ?? "");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Mcp:Endpoint", "http://localhost/mcp");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmployeeRepository>();
            services.AddSingleton<IEmployeeRepository>(Repository);

            if (UseScriptedAi)
            {
                services.RemoveAll<IAIProviderService>();
                services.AddSingleton<IAIProviderService>(Ai);
            }

            // The chat host's MCP client talks to the in-memory server.
            services.AddHttpClient(McpToolInvoker.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler());
        });
    }
}

public class McpIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;

    public McpIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
        _http = factory.CreateClient();
    }

    private async Task<JsonElement> Chat(object body)
    {
        var response = await _http.PostAsJsonAsync("/api/chat", body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static object UserMessage(string text) =>
        new { messages = new[] { new { role = "user", content = text } } };

    [Fact]
    public async Task Mcp_endpoint_lists_all_employee_tools_with_annotations()
    {
        await using var client = await McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri("http://localhost/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
            _factory.Server.CreateClient()));

        var tools = (await client.ListToolsAsync()).ToDictionary(t => t.Name);

        foreach (var name in new[]
                 {
                     "list_employees", "find_employees", "get_employee", "create_employee", "update_employee", "delete_employee",
                     "bulk_create_employees", "bulk_update_employees", "bulk_delete_employees", "list_departments", "get_salary_stats"
                 })
            Assert.True(tools.ContainsKey(name), $"missing tool {name}");

        Assert.True(tools["delete_employee"].ProtocolTool.Annotations?.DestructiveHint);
        Assert.True(tools["bulk_delete_employees"].ProtocolTool.Annotations?.DestructiveHint);
        Assert.True(tools["list_employees"].ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.NotEqual(true, tools["create_employee"].ProtocolTool.Annotations?.DestructiveHint);
    }

    [Fact]
    public async Task Create_via_chat_runs_the_tool_and_flags_a_refresh()
    {
        _factory.Ai
            .CallTool("create_employee", new { fullName = "Jane Doe", email = "jane.create@x.com", department = "IT", salary = 50000 })
            .Say("Added Jane Doe to IT.");

        var r = await Chat(UserMessage("add Jane Doe, jane.create@x.com, IT, 50000"));

        Assert.Equal("SUCCESS", r.GetProperty("type").GetString());
        Assert.True(r.GetProperty("dataChanged").GetBoolean());
        Assert.Equal("Jane Doe", r.GetProperty("data").GetProperty("employee").GetProperty("fullName").GetString());
        Assert.Equal(3, r.GetProperty("newMessages").GetArrayLength()); // assistant tool call, tool result, final text
        var tool = r.GetProperty("newMessages")[1];
        Assert.Equal("tool", tool.GetProperty("role").GetString());
        Assert.NotNull(_factory.Repository.GetAllAsync(1, 100, "jane.create@x.com").Result.items.SingleOrDefault());
    }

    [Fact]
    public async Task Validation_error_reaches_the_model_as_a_tool_error()
    {
        _factory.Ai
            .CallTool("create_employee", new { fullName = "Bad Mail", email = "not-an-email", department = "IT", salary = 100 })
            .Say("That email isn't valid.");

        var r = await Chat(UserMessage("add Bad Mail not-an-email IT 100"));

        var secondCall = _factory.Ai.Calls[^1];
        Assert.Contains(secondCall, m => m.Role == "tool" && m.Content!.StartsWith("ERROR:") && m.Content.Contains("Invalid email"));
        Assert.Equal("ERROR", r.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Delete_needs_a_confirmation_token_and_survives_the_json_round_trip()
    {
        var jane = _factory.Repository.Seed("Delete Target", "delete.target@x.com", "Sales", 40000);
        _factory.Ai.CallTool("delete_employee", new { id = jane.Id });

        // Turn 1: the model asks to delete → the server holds the action and nothing is deleted.
        var first = await Chat(UserMessage("delete Delete Target"));
        Assert.Equal("CONFIRMATION", first.GetProperty("type").GetString());
        Assert.NotNull(_factory.Repository.Find(jane.Id));
        var token = first.GetProperty("pendingAction").GetProperty("token").GetString();
        Assert.Contains("Delete Target", first.GetProperty("pendingAction").GetProperty("summary").GetString());

        // Turn 2: the user presses Confirm → the token is echoed back; no model call is made.
        var callsBefore = _factory.Ai.Calls.Count;
        var second = await Chat(new { confirmAction = token });
        Assert.Equal("SUCCESS", second.GetProperty("type").GetString());
        Assert.True(second.GetProperty("dataChanged").GetBoolean());
        Assert.Null(_factory.Repository.Find(jane.Id));
        Assert.Equal(callsBefore, _factory.Ai.Calls.Count);

        // Replaying the same token later finds nothing left to delete and does not touch other data.
        var replay = await Chat(new { confirmAction = token });
        Assert.Equal("ERROR", replay.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Forged_confirmation_token_is_rejected()
    {
        var keep = _factory.Repository.Seed("Keep Me", "keep.me@x.com", "Ops", 1000);

        var r = await Chat(new { confirmAction = "CfDJ8-forged-token" });

        Assert.Equal("ERROR", r.GetProperty("type").GetString());
        Assert.NotNull(_factory.Repository.Find(keep.Id));
    }

    [Fact]
    public async Task Bulk_create_reports_each_row()
    {
        _factory.Ai
            .CallTool("bulk_create_employees", new
            {
                employees = new object[]
                {
                    new { fullName = "Bulk One", email = "bulk.one@x.com", department = "HR", salary = 1000 },
                    new { fullName = "Bulk Two", email = "bulk.one@x.com", department = "HR", salary = 2000 }   // duplicate email
                }
            })
            .Say("One created, one failed.");

        var r = await Chat(UserMessage("add these two"));

        var data = r.GetProperty("data");
        Assert.Equal(1, data.GetProperty("successCount").GetInt32());
        Assert.Equal(1, data.GetProperty("failCount").GetInt32());
        Assert.Contains("already exists", data.GetProperty("results")[1].GetProperty("error").GetString());
    }

    [Fact]
    public async Task Salary_stats_tool_answers_aggregate_questions()
    {
        _factory.Repository.Seed("Stat A", "stat.a@x.com", "StatDept", 100);
        _factory.Repository.Seed("Stat B", "stat.b@x.com", "StatDept", 300);
        _factory.Ai.CallTool("get_salary_stats", new { department = "statdept" }).Say("Average is 200.");

        var r = await Chat(UserMessage("average salary in StatDept"));

        var stats = r.GetProperty("data").GetProperty("stats");
        Assert.Equal(2, stats.GetProperty("employeeCount").GetInt32());
        Assert.Equal(200m, stats.GetProperty("averageSalary").GetDecimal());
    }

    [Fact]
    public async Task Update_tool_returns_before_and_after()
    {
        var e = _factory.Repository.Seed("Raise Me", "raise.me@x.com", "IT", 1000);
        _factory.Ai.CallTool("update_employee", new { id = e.Id, salary = 1100 }).Say("Done.");

        var r = await Chat(UserMessage("10% raise for Raise Me"));

        var changes = r.GetProperty("data").GetProperty("changes");
        Assert.Equal("salary", changes[0].GetProperty("field").GetString());
        Assert.Equal("1000", changes[0].GetProperty("from").GetString());
        Assert.Equal("1100", changes[0].GetProperty("to").GetString());
    }

    [Fact]
    public async Task Chat_rejects_an_empty_request()
    {
        var response = await _http.PostAsJsonAsync("/api/chat", new { messages = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Departments_endpoint_is_available_for_the_frontend()
    {
        _factory.Repository.Seed("Dept Person", "dept.person@x.com", "Zeta", 1);

        var departments = await _http.GetFromJsonAsync<List<string>>("/api/employees/departments");

        Assert.Contains("Zeta", departments!);
    }
}
