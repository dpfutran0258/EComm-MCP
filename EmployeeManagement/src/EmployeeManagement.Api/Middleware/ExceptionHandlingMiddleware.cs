using System.Net;
using System.Text.Json;
using FluentValidation;
using EmployeeManagement.Application.Exceptions;

namespace EmployeeManagement.Api.Middleware;

/// <summary>
/// Global exception handler middleware.
/// Catches all unhandled exceptions and returns a consistent <see cref="ApiErrorResponse"/>.
/// Controllers no longer need try/catch blocks for common exception types.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
                         ?? context.TraceIdentifier;

        var (statusCode, message, errors) = exception switch
        {
            ValidationException vex => (
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                vex.Errors.ToDictionary(e => e.PropertyName, e => new[] { e.ErrorMessage })),

            NotFoundException nfex => (
                HttpStatusCode.NotFound,
                nfex.Message,
                (Dictionary<string, string[]>?)null),

            ArgumentException aex => (
                HttpStatusCode.BadRequest,
                aex.Message,
                null),

            HttpRequestException hrex => (
                HttpStatusCode.BadGateway,
                "An upstream service request failed.",
                null),

            OperationCanceledException => (
                HttpStatusCode.ServiceUnavailable,
                "The request was cancelled.",
                null),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred.",
                null)
        };

        // Log at the appropriate level
        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);
        else if (statusCode != HttpStatusCode.NotFound)
            _logger.LogWarning(exception, "Handled exception {Type}. CorrelationId={CorrelationId}", exception.GetType().Name, correlationId);

        var response = new ApiErrorResponse
        {
            Message = message,
            StatusCode = (int)statusCode,
            CorrelationId = correlationId,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>Standardised error response returned by all API endpoints.</summary>
public sealed class ApiErrorResponse
{
    public string Message { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public string? CorrelationId { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
    public DateTime Timestamp { get; init; }
}
