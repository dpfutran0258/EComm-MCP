using Serilog.Context;

namespace EmployeeManagement.Api.Middleware;

/// <summary>
/// Ensures every request has an X-Correlation-ID header.
/// If the caller provides one, it is reused; otherwise a new GUID is generated.
/// The ID is added to the response so clients can trace requests.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                         ?? Guid.NewGuid().ToString();

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
