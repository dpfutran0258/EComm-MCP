using System.Net.Http.Json;
using System.Text.Json;

namespace EmployeeManagement.Tests;

/// <summary>Hosts the API with the REAL OpenRouter model (in-memory data, no database).</summary>
public sealed class LiveApiFactory : ApiFactory
{
    protected override bool UseScriptedAi => false;
}

/// <summary>
/// Optional end-to-end check that the real model chooses the right MCP tools and uses the transcript as context.
/// Skipped unless EM_OPENROUTER_KEY is set, because it calls the external LLM (slow, non-deterministic).
/// <code>
/// $env:EM_OPENROUTER_KEY = '&lt;key&gt;'; dotnet test --filter LiveLlmSmokeTests
/// </code>
/// </summary>
public class LiveLlmSmokeTests : IClassFixture<LiveApiFactory>
{
    private readonly LiveApiFactory _factory;
    private readonly List<JsonElement> _transcript = new();

    public LiveLlmSmokeTests(LiveApiFactory factory) => _factory = factory;

    private static bool Enabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EM_OPENROUTER_KEY"));

    private async Task<JsonElement> Say(string text)
    {
        _transcript.Add(JsonSerializer.SerializeToElement(new { role = "user", content = text }));

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/chat", new { messages = _transcript });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);

        var json = JsonDocument.Parse(body).RootElement.Clone();
        foreach (var m in json.GetProperty("newMessages").EnumerateArray())
            _transcript.Add(m.Clone());
        return json;
    }

    [Fact]
    public async Task Real_model_creates_updates_with_context_and_asks_before_deleting()
    {
        if (!Enabled) return; // opt-in

        // 1. Raw chat → create_employee
        var created = await Say("Add Maria Lopez, maria.lopez@example.com, Engineering, 80000");
        Assert.True(created.GetProperty("dataChanged").GetBoolean(), created.GetRawText());
        Assert.NotNull(_factory.Repository.GetAllAsync(1, 10, "maria.lopez@example.com").Result.items.SingleOrDefault());

        // 2. Follow-up that only makes sense with context ("her", "that") → read salary, compute, update
        var raised = await Say("give her a 10% raise");
        Assert.True(raised.GetProperty("dataChanged").GetBoolean(), raised.GetRawText());
        var maria = _factory.Repository.GetAllAsync(1, 10, "maria").Result.items.Single();
        Assert.Equal(88000m, maria.Salary);

        // 3. Aggregate question → get_salary_stats (read-only)
        var stats = await Say("what is the average salary in engineering?");
        Assert.False(stats.GetProperty("dataChanged").GetBoolean(), stats.GetRawText());
        Assert.Contains("88", stats.GetProperty("responseText").GetString());

        // 4. Delete → the application must hold the action for confirmation
        var delete = await Say("remove her from the system");
        Assert.Equal("CONFIRMATION", delete.GetProperty("type").GetString());
        Assert.NotNull(_factory.Repository.Find(maria.Id));
    }
}
