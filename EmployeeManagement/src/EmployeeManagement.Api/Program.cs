using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using EmployeeManagement.Api.Mcp;
using EmployeeManagement.Api.Middleware;
using EmployeeManagement.Application.AI;
using EmployeeManagement.Application.AI.Providers;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Application.Services;
using EmployeeManagement.Infrastructure.Persistence;
using EmployeeManagement.Infrastructure.Persistence.Repositories;

// ── Serilog bootstrap logger (captures startup errors) ───────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Structured logging (Serilog) ─────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

    // ── CORS ─────────────────────────────────────────────────────────────────
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://localhost:5173", "http://localhost:3000" };

    builder.Services.AddCors(opts =>
        opts.AddPolicy("FrontendPolicy", policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .SetPreflightMaxAge(TimeSpan.FromHours(1))));

    // ── Controllers + JSON ───────────────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new() { Title = "Employee Management API", Version = "v1" });
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "EmployeeManagement.Api.xml");
        if (File.Exists(xmlPath))
            opts.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    });

    // ── Database ─────────────────────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "Connection string 'DefaultConnection' is required. Set it with " +
            "'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<value>\"'.");

    builder.Services.AddDbContext<EmployeeManagementDbContext>(opts =>
        opts.UseNpgsql(connectionString)
            .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
            .LogTo(message => Log.Debug("{Sql}", message), LogLevel.Debug));

    // ── AI Provider Options ───────────────────────────────────────────────────
    // Validated at startup (ValidateOnStart) so misconfiguration fails fast instead of on the first chat.
    builder.Services.AddOptions<AIOptions>()
        .Bind(builder.Configuration.GetSection(AIOptions.SectionName))
        .Validate(o => !string.IsNullOrWhiteSpace(o.OpenRouter.ApiKey),
            "AI:OpenRouter:ApiKey is required. Set it with 'dotnet user-secrets set \"AI:OpenRouter:ApiKey\" \"<key>\"'.")
        .Validate(o => !string.IsNullOrWhiteSpace(o.OpenRouter.Model),
            "AI:OpenRouter:Model is required.")
        .Validate(o => Uri.TryCreate(o.OpenRouter.BaseUrl, UriKind.Absolute, out var uri)
                       && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
            "AI:OpenRouter:BaseUrl must be an absolute http(s) URI.")
        .Validate(o => o.OpenRouter.TimeoutSeconds is >= 1 and <= 300,
            "AI:OpenRouter:TimeoutSeconds must be between 1 and 300.")
        .Validate(o => o.OpenRouter.MaxOutputTokens > 0,
            "AI:OpenRouter:MaxOutputTokens must be greater than 0.")
        .ValidateOnStart();

    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AIOptions>>().Value);
    builder.Services.AddSingleton(sp => sp.GetRequiredService<AIOptions>().OpenRouter);

    // ── AI Provider HttpClients + registrations ──────────────────────────────
    // Named HttpClients — IHttpClientFactory manages socket pooling correctly.
    // The provider sets HttpClient.Timeout from the validated options.
    builder.Services.AddHttpClient("OpenRouter");

    // Register OpenRouter as the single AI provider (no factory needed)
    builder.Services.AddSingleton<IAIProviderService>(sp =>
    {
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var logger  = sp.GetRequiredService<ILogger<OpenRouterAIProvider>>();
        var options = sp.GetRequiredService<OpenRouterProviderOptions>();
        return new OpenRouterAIProvider(options, logger, factory.CreateClient("OpenRouter"));
    });

    // ── Application Services ──────────────────────────────────────────────────
    builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
    builder.Services.AddScoped<IEmployeeService, EmployeeService>();

    // ── MCP server (employee tools) + chat host (MCP client + AI agent loop) ──
    // The MCP server is stateless: every request is independent, no session to expire.
    builder.Services.AddMcpServer()
        .WithHttpTransport(o => o.Stateless = true)
        .WithTools<EmployeeTools>();

    builder.Services.AddOptions<McpClientOptions>()
        .Bind(builder.Configuration.GetSection(McpClientOptions.SectionName))
        .Validate(o => Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var uri)
                       && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
            "Mcp:Endpoint must be an absolute http(s) URI.")
        .ValidateOnStart();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<McpClientOptions>>().Value);
    builder.Services.AddHttpClient(McpToolInvoker.HttpClientName);

    builder.Services.AddDataProtection();
    builder.Services.AddSingleton<IPendingActionProtector, PendingActionProtector>();
    builder.Services.AddScoped<IToolInvoker, McpToolInvoker>();
    builder.Services.AddScoped<ChatAgentService>();

    // ── Health Checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // ── Resolve AI provider first (validates AI options before touching the DB) ─
    var aiProvider = app.Services.GetRequiredService<IAIProviderService>();
    Log.Information("AI provider ready: {Provider}", aiProvider.ProviderName);

    // ── Auto-migrate database ─────────────────────────────────────────────────
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<EmployeeManagementDbContext>();
        db.Database.Migrate();
        Log.Information("Database migration applied.");
    }

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseMiddleware<CorrelationIdMiddleware>();
    // CORS must wrap the exception handler, otherwise error responses (400/404/500) carry no CORS headers
    // and the browser reports them as network errors.
    app.UseCors("FrontendPolicy");
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // The MCP endpoint can create and delete data and has no authentication yet: loopback clients only.
    app.UseWhen(
        ctx => ctx.Request.Path.StartsWithSegments("/mcp"),
        branch => branch.Use(async (ctx, next) =>
        {
            var remote = ctx.Connection.RemoteIpAddress;
            if (remote is not null && !System.Net.IPAddress.IsLoopback(remote))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next();
        }));

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0}ms";
    });

    app.UseAuthorization();
    app.MapControllers();
    app.MapMcp("/mcp");
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException) // HostAbortedException is expected under 'dotnet ef'
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

// Makes the entry point visible to WebApplicationFactory in the test project.
public partial class Program;
