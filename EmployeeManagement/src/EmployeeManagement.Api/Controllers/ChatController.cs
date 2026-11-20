using Microsoft.AspNetCore.Mvc;
using EmployeeManagement.Application.AI;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Services;

namespace EmployeeManagement.Api.Controllers;

/// <summary>
/// AI chat endpoint. The client sends the raw conversation (including earlier tool calls and results);
/// <see cref="ChatAgentService"/> lets the model pick MCP tools and returns the new transcript messages.
/// The server keeps no conversation state.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ChatController : ControllerBase
{
    /// <summary>Request body limit: the transcript is sent with every message.</summary>
    private const long MaxRequestBytes = 512 * 1024;

    private readonly ChatAgentService _chat;
    private readonly OpenRouterProviderOptions _providerOptions;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatAgentService chat, OpenRouterProviderOptions providerOptions, ILogger<ChatController> logger)
    {
        _chat = chat;
        _providerOptions = providerOptions;
        _logger = logger;
    }

    /// <summary>
    /// Sends the conversation and receives the assistant's reply.
    /// Set <c>confirmAction</c> (token from a previous <c>pendingAction</c>) to run a confirmed destructive action.
    /// </summary>
    /// <response code="200">Processed. See <c>type</c>: SUCCESS, ERROR, CLARIFICATION, INFO or CONFIRMATION.</response>
    /// <response code="400">Invalid request (no messages, last message not from the user, or a model that is not allowed).</response>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponse>> Send([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            throw new ArgumentException("Request body is required.");

        if (!string.IsNullOrWhiteSpace(request.PreferredModel) && !_providerOptions.IsModelAllowed(request.PreferredModel))
            throw new ArgumentException("PreferredModel is not allowed.");

        if (string.IsNullOrWhiteSpace(request.ConfirmAction) && (request.Messages is null || request.Messages.Count == 0))
            throw new ArgumentException("Messages is required.");

        _logger.LogInformation(
            "Chat request. Messages={Count}, Confirm={Confirm}, Model={Model}",
            request.Messages?.Count ?? 0, !string.IsNullOrWhiteSpace(request.ConfirmAction), request.PreferredModel ?? "default");

        var response = await _chat.ProcessAsync(request, cancellationToken);
        return Ok(response);
    }
}
