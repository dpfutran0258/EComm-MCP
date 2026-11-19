using Microsoft.AspNetCore.Mvc;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Services;

namespace EmployeeManagement.Api.Controllers;

/// <summary>
/// Handles AI-powered chat interactions for natural-language employee management.
/// All conversation state is managed by ChatService — this controller only validates
/// the HTTP request and delegates.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Sends a user message and receives an AI-generated response.
    /// The response may describe an employee operation that was executed,
    /// ask a clarifying question, or request confirmation for destructive actions.
    /// </summary>
    /// <param name="request">Message, session ID, context, and optional provider override.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Message processed successfully.</response>
    /// <response code="400">Invalid request (empty message or missing session ID).</response>
    [HttpPost("message")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponse>> SendMessage(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Request body is required." });

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty." });

        if (string.IsNullOrWhiteSpace(request.SessionId))
            return BadRequest(new { error = "SessionId is required." });

        _logger.LogInformation(
            "Chat request. Session={SessionId}, MessageLength={Len}, Provider={Provider}, ContextKeys={ContextKeys}",
            request.SessionId,
            request.Message.Length,
            request.PreferredProvider ?? "default",
            request.Context != null && request.Context.Count > 0 
                ? string.Join(", ", request.Context.Keys) 
                : "empty");

        if (request.Context != null && request.Context.Count > 0)
        {
            foreach (var (key, value) in request.Context)
            {
                _logger.LogDebug("Context[{Key}] = {Value} (Type: {Type})", 
                    key, value, value?.GetType().Name ?? "null");
            }
        }

        var response = await _chatService.ProcessMessageAsync(
            request.Message,
            request.SessionId,
            request.Context ?? new Dictionary<string, object>(),
            request.PreferredProvider,
            request.PreferredModel,
            cancellationToken);

        return Ok(response);
    }
}
