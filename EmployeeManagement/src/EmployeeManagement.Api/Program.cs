using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
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
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

    builder.Services.AddDbContext<EmployeeManagementDbContext>(opts =>
        opts.UseNpgsql(connectionString)
            .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
            .LogTo(message => Log.Debug("{Sql}", message), LogLevel.Debug));

    // ── AI Provider Options ───────────────────────────────────────────────────
    var aiOptions = builder.Configuration.GetSection(AIOptions.SectionName).Get<AIOptions>()
                 ?? new AIOptions();

    builder.Services.AddSingleton(aiOptions);
    builder.Services.AddSingleton(aiOptions.OpenRouter);

    // ── AI Provider HttpClients + registrations ──────────────────────────────
    // Named HttpClients — IHttpClientFactory manages socket pooling correctly.
    builder.Services.AddHttpClient("OpenRouter", client =>
        client.Timeout = TimeSpan.FromSeconds(aiOptions.OpenRouter.TimeoutSeconds));

    // Register OpenRouter as the single AI provider (no factory needed)
    builder.Services.AddSingleton<IAIProviderService>(sp =>
    {
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var logger  = sp.GetRequiredService<ILogger<OpenRouterAIProvider>>();
        return new OpenRouterAIProvider(aiOptions.OpenRouter, logger, factory.CreateClient("OpenRouter"));
    });

    // ── Application Services ──────────────────────────────────────────────────
    builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
    builder.Services.AddScoped<IEmployeeService, EmployeeService>();
    builder.Services.AddSingleton<IConversationService, ConversationService>();
    builder.Services.AddSingleton<TableParsingService>();
    builder.Services.AddScoped<ChatService>();

    // ── Health Checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // ── Auto-migrate database ─────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<EmployeeManagementDbContext>();
        db.Database.Migrate();
        Log.Information("Database migration applied.");
    }

    // ── Log AI provider at startup ────────────────────────────────────────────
    var aiProvider = app.Services.GetRequiredService<IAIProviderService>();
    Log.Information("AI provider ready: {Provider}", aiProvider.ProviderName);

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();

    app.UseCors("FrontendPolicy");
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0}ms";
    });

    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start.");
}
finally
{
    Log.CloseAndFlush();
}
