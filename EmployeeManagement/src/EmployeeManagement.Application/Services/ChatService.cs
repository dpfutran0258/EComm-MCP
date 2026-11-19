using FluentValidation;
using EmployeeManagement.Application.AI;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Exceptions;
using EmployeeManagement.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EmployeeManagement.Application.Services;

/// <summary>
/// Orchestrates the full chat request lifecycle:
///   1. Retrieve conversation history
///   2. Build employee context
///   3. Call the AI provider (OpenRouter)
///   4. Route to the appropriate employee operation
///   5. Return a ChatResponse with AI-generated text
///
/// Business rules and DB operations live in <see cref="IEmployeeService"/>.
/// AI communication is handled by OpenRouter provider directly.
/// This class only coordinates — it contains no SQL and no HTTP calls.
/// </summary>
public sealed class ChatService
{
    private readonly IAIProviderService _aiProvider;
    private readonly IEmployeeService _employeeService;
    private readonly IConversationService _conversationService;
    private readonly TableParsingService _tableParser;
    private readonly ILogger<ChatService> _logger;

    // Minimum confidence to act on the detected intent
    private const decimal ConfidenceThreshold = 0.6m;

    // Maximum employees sent to AI as context
    private const int MaxContextEmployees = 20;

    // Conversation history turns sent to AI (reduced to save tokens)
    private const int MaxHistoryMessages = 6;

    public ChatService(
        IAIProviderService aiProvider,
        IEmployeeService employeeService,
        IConversationService conversationService,
        TableParsingService tableParser,
        ILogger<ChatService> logger)
    {
        _aiProvider = aiProvider;
        _employeeService = employeeService;
        _conversationService = conversationService;
        _tableParser = tableParser;
        _logger = logger;
    }

    /// <summary>
    /// Entry point for all chat messages from the controller.
    /// </summary>
    public async Task<ChatResponse> ProcessMessageAsync(
        string userMessage,
        string sessionId,
        Dictionary<string, object> context,
        string? preferredProvider = null,
        string? preferredModel = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return BuildErrorResponse("Please enter a message.", context);

        _logger.LogInformation(
            "Chat message received. Session={SessionId}, Message={Message}",
            sessionId, userMessage);
        
        // Log incoming context
        if (context.Count > 0)
        {
            _logger.LogDebug("Incoming context: {Context}", 
                string.Join(", ", context.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }
        else
        {
            _logger.LogWarning("⚠️ Incoming context is EMPTY for session {SessionId}", sessionId);
        }

        try
        {
            // 1. Get conversation history
            var history = await _conversationService.GetHistoryAsync(sessionId, MaxHistoryMessages);
            
            _logger.LogDebug("Retrieved {Count} conversation history messages for session {SessionId}", 
                history.Count, sessionId);

            // 2. Get relevant employee context (not all employees)
            var employeeContext = await GetRelevantContextAsync(userMessage);

            // 3. Call AI (OpenRouter)
            var aiRequest = new AIRequest
            {
                UserMessage = userMessage,
                SessionId = sessionId,
                History = history,
                EmployeeContext = employeeContext,
                PreferredProvider = preferredProvider,
                PreferredModel = preferredModel
            };

            var aiResponse = await _aiProvider.ProcessChatAsync(aiRequest, cancellationToken);

            _logger.LogInformation(
                "AI response. Session={SessionId}, Provider={Provider}, Model={Model}, Intent={Intent}, Confidence={Confidence}, Duration={Duration}ms",
                sessionId,
                aiResponse.Metadata.Provider,
                aiResponse.Metadata.Model,
                aiResponse.Intent,
                aiResponse.Confidence,
                (int)aiResponse.Metadata.Duration.TotalMilliseconds);

            // 4. Route to handler
            ChatResponse chatResponse;

            if (aiResponse.Confidence < ConfidenceThreshold)
            {
                chatResponse = new ChatResponse
                {
                    ResponseText = aiResponse.ResponseText,
                    Type = ChatResponseType.CLARIFICATION,
                    Data = new { suggestions = aiResponse.Suggestions },
                    UpdatedContext = context,
                    ProviderUsed = aiResponse.Metadata.Provider,
                    ModelUsed = aiResponse.Metadata.Model
                };
            }
            else
            {
                chatResponse = await RouteIntentAsync(aiResponse, userMessage, sessionId, context, cancellationToken);
                chatResponse = chatResponse with
                {
                    ProviderUsed = aiResponse.Metadata.Provider,
                    ModelUsed = aiResponse.Metadata.Model
                };
            }

            // 5. Persist conversation turn
            await _conversationService.AddMessageAsync(sessionId, new ConversationMessage
            {
                Role = "user",
                Content = userMessage,
                Timestamp = DateTime.UtcNow
            });
            await _conversationService.AddMessageAsync(sessionId, new ConversationMessage
            {
                Role = "assistant",
                Content = chatResponse.ResponseText,
                Timestamp = DateTime.UtcNow
            });

            // Log outgoing context
            if (chatResponse.UpdatedContext.Count > 0)
            {
                _logger.LogDebug("Outgoing context: {Context}", 
                    string.Join(", ", chatResponse.UpdatedContext.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }
            else
            {
                _logger.LogWarning("⚠️ Outgoing context is EMPTY for session {SessionId}", sessionId);
            }

            return chatResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ChatService. Session={SessionId}", sessionId);
            return BuildErrorResponse("An unexpected error occurred. Please try again.");
        }
    }

    // ── Intent Router ─────────────────────────────────────────────────────────

    private async Task<ChatResponse> RouteIntentAsync(
        AIResponse ai,
        string userMessage,
        string sessionId,
        Dictionary<string, object> context,
        CancellationToken ct)
    {
        return ai.Intent switch
        {
            ChatIntent.GREETING => BuildResponse(ai.ResponseText, ChatResponseType.INFO, null, context),
            ChatIntent.HELP => BuildHelpResponse(context),
            ChatIntent.CREATE_EMPLOYEE => await HandleCreateAsync(ai, context, ct),
            ChatIntent.READ_EMPLOYEE => await HandleReadAsync(ai, context, ct),
            ChatIntent.READ_ALL_EMPLOYEES => await HandleReadAllAsync(ai, context, ct),
            ChatIntent.UPDATE_EMPLOYEE => await HandleUpdateAsync(ai, context, ct),
            ChatIntent.DELETE_EMPLOYEE => await HandleDeleteAsync(userMessage, ai, context, ct),
            ChatIntent.SEARCH_EMPLOYEES => await HandleSearchAsync(ai, context, ct),
            ChatIntent.BULK_CREATE_EMPLOYEES => await HandleBulkCreateAsync(userMessage, ai, context, ct),
            ChatIntent.BULK_UPDATE_EMPLOYEES => await HandleBulkUpdateAsync(userMessage, ai, context, ct),
            ChatIntent.BULK_DELETE_EMPLOYEES => await HandleBulkDeleteAsync(userMessage, ai, context, ct),
            ChatIntent.CONFIRMATION => await HandleConfirmationAsync(userMessage, ai, context, ct),
            _ => BuildResponse(ai.ResponseText, ChatResponseType.INFO, new { suggestions = ai.Suggestions }, context)
        };
    }

    // ── Single Employee Handlers ──────────────────────────────────────────────

    private async Task<ChatResponse> HandleCreateAsync(AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        var e = ai.Entities;

        if (string.IsNullOrWhiteSpace(e.FullName))
            return BuildResponse(ai.ResponseText, ChatResponseType.CLARIFICATION, null, ctx);
        if (string.IsNullOrWhiteSpace(e.Email))
            return BuildResponse(ai.ResponseText, ChatResponseType.CLARIFICATION, null, ctx);
        if (string.IsNullOrWhiteSpace(e.Department))
            return BuildResponse(ai.ResponseText, ChatResponseType.CLARIFICATION, null, ctx);
        if (!e.Salary.HasValue || e.Salary <= 0)
            return BuildResponse(ai.ResponseText, ChatResponseType.CLARIFICATION, null, ctx);

        try
        {
            var dto = new CreateEmployeeDto
            {
                FullName = e.FullName,
                Email = e.Email,
                Department = e.Department,
                Salary = e.Salary.Value
            };
            var created = await _employeeService.CreateAsync(dto);

            _logger.LogInformation("Employee created via chat. Id={Id}, Name={Name}", created.Id, created.FullName);
            
            // Store only ID in context (avoid serialization issues)
            ctx["lastCreatedEmployeeId"] = created.Id;

            return BuildResponse(
                $"✅ {created.FullName} has been successfully added to the system.",
                ChatResponseType.SUCCESS,
                new { employee = created, needsRefresh = true },
                ctx);
        }
        catch (ValidationException vex)
        {
            var errors = vex.Errors.Select(x => x.ErrorMessage).ToList();
            _logger.LogWarning("Validation failed creating employee: {Errors}", string.Join("; ", errors));
            return BuildErrorResponse($"I couldn't create the employee: {string.Join(", ", errors)}", ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating employee via chat.");
            return BuildErrorResponse("Failed to create employee. Please try again.", ctx);
        }
    }

    private async Task<ChatResponse> HandleReadAsync(AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        var e = ai.Entities;
        EmployeeDto? employee = null;

        _logger.LogDebug(
            "HandleReadAsync: EmployeeId={EmployeeId}, FullName={FullName}, Email={Email}, SearchQuery={SearchQuery}",
            e.EmployeeId, e.FullName, e.Email, e.SearchQuery);

        // Try by ID first
        if (e.EmployeeId.HasValue)
        {
            try
            {
                employee = await _employeeService.GetByIdAsync(e.EmployeeId.Value);
                _logger.LogDebug("Found employee by ID: {Id} - {Name}", employee.Id, employee.FullName);
            }
            catch (NotFoundException)
            {
                _logger.LogDebug("Employee not found by ID: {Id}", e.EmployeeId.Value);
            }
        }

        // Try by name, email, or search query
        if (employee == null)
        {
            var search = e.FullName ?? e.Email ?? e.SearchQuery;
            if (!string.IsNullOrWhiteSpace(search))
            {
                _logger.LogDebug("Searching for employee by: {Search}", search);
                var results = await _employeeService.GetAllAsync(1, 100, search, null);
                _logger.LogDebug("Search returned {Count} results", results.Data.Count);
                
                if (results.Data.Count == 0)
                {
                    return BuildErrorResponse($"No employee found matching '{search}'.", ctx);
                }
                
                if (results.Data.Count == 1)
                {
                    employee = results.Data[0];
                    _logger.LogDebug("Found exact match: {Id} - {Name}", employee.Id, employee.FullName);
                }
                else
                {
                    _logger.LogDebug("Multiple matches found");
                    return BuildResponse(
                        $"I found {results.Data.Count} employees matching '{search}':\n" +
                        string.Join("\n", results.Data.Select(emp => $"• {emp.FullName} ({emp.Email}) - {emp.Department} - ${emp.Salary:N0}")),
                        ChatResponseType.CLARIFICATION,
                        new { matches = results.Data },
                        ctx);
                }
            }
            else
            {
                _logger.LogDebug("No search criteria provided, checking context");
            }
        }

        // Try from last created employee in context (by ID)
        if (employee == null && ctx.TryGetValue("lastCreatedEmployeeId", out var lastCreatedIdObj) && lastCreatedIdObj is int or long)
        {
            var createdId = Convert.ToInt32(lastCreatedIdObj);
            try
            {
                employee = await _employeeService.GetByIdAsync(createdId);
                _logger.LogDebug("Using lastCreatedEmployeeId from context: {Id}", createdId);
            }
            catch (NotFoundException)
            {
                _logger.LogDebug("lastCreatedEmployeeId {Id} no longer exists", createdId);
            }
        }

        if (employee == null)
        {
            return BuildResponse(
                "I couldn't find that employee. Please provide their name, email, or ID.",
                ChatResponseType.CLARIFICATION,
                null,
                ctx);
        }

        ctx["lastViewedEmployeeId"] = employee.Id;
        return BuildResponse(
            $"Here are the details for {employee.FullName}:\n" +
            $"• **Email:** {employee.Email}\n" +
            $"• **Department:** {employee.Department}\n" +
            $"• **Salary:** ${employee.Salary:N0}\n" +
            $"• **Employee ID:** {employee.Id}",
            ChatResponseType.SUCCESS,
            new { employee },
            ctx);
    }

    private async Task<ChatResponse> HandleReadAllAsync(AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        var results = await _employeeService.GetAllAsync(1, 100);
        if (results.Data.Count == 0)
            return BuildResponse(ai.ResponseText, ChatResponseType.INFO, null, ctx);

        return BuildResponse(ai.ResponseText, ChatResponseType.SUCCESS,
            new { employees = results.Data, totalCount = results.TotalCount }, ctx);
    }

    private async Task<ChatResponse> HandleUpdateAsync(AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        var e = ai.Entities;
        EmployeeDto? employee = null;

        _logger.LogDebug(
            "HandleUpdateAsync: EmployeeId={EmployeeId}, FullName={FullName}, Email={Email}, Department={Department}, Salary={Salary}",
            e.EmployeeId, e.FullName, e.Email, e.Department, e.Salary);

        // Try by ID first
        if (e.EmployeeId.HasValue)
        {
            try
            {
                employee = await _employeeService.GetByIdAsync(e.EmployeeId.Value);
                _logger.LogDebug("Found employee by ID: {Id} - {Name}", employee.Id, employee.FullName);
            }
            catch (NotFoundException)
            {
                _logger.LogDebug("Employee not found by ID: {Id}", e.EmployeeId.Value);
            }
        }

        // Try by name or email
        if (employee == null)
        {
            var search = e.FullName ?? e.Email;
            if (!string.IsNullOrWhiteSpace(search))
            {
                _logger.LogDebug("Searching for employee by: {Search}", search);
                var results = await _employeeService.GetAllAsync(1, 50, search, null);
                _logger.LogDebug("Search returned {Count} results", results.Data.Count);
                
                if (results.Data.Count == 1)
                {
                    employee = results.Data[0];
                    _logger.LogDebug("Found exact match: {Id} - {Name}", employee.Id, employee.FullName);
                }
                else if (results.Data.Count > 1)
                {
                    _logger.LogDebug("Multiple matches found, need clarification");
                    return BuildResponse(
                        $"I found {results.Data.Count} employees matching '{search}'. Please be more specific:\n" +
                        string.Join("\n", results.Data.Select(emp => $"• {emp.FullName} ({emp.Email}) - ID: {emp.Id}")),
                        ChatResponseType.CLARIFICATION,
                        new { matches = results.Data },
                        ctx);
                }
            }
        }

        // Try from last viewed/created employee in context (by ID)
        if (employee == null && ctx.TryGetValue("lastCreatedEmployeeId", out var lastCreatedIdObj) && lastCreatedIdObj is int or long)
        {
            var createdId = Convert.ToInt32(lastCreatedIdObj);
            try
            {
                employee = await _employeeService.GetByIdAsync(createdId);
                _logger.LogDebug("Using lastCreatedEmployeeId from context: {Id}", createdId);
            }
            catch (NotFoundException)
            {
                _logger.LogDebug("lastCreatedEmployeeId {Id} no longer exists", createdId);
            }
        }

        if (employee == null && ctx.TryGetValue("lastViewedEmployeeId", out var lastViewedIdObj) && lastViewedIdObj is int or long)
        {
            var viewedId = Convert.ToInt32(lastViewedIdObj);
            try
            {
                employee = await _employeeService.GetByIdAsync(viewedId);
                _logger.LogDebug("Using lastViewedEmployeeId from context: {Id}", viewedId);
            }
            catch (NotFoundException)
            {
                _logger.LogDebug("lastViewedEmployeeId {Id} no longer exists", viewedId);
            }
        }

        if (employee == null)
        {
            _logger.LogWarning("Could not identify employee for update");
            return BuildResponse(
                "I couldn't identify which employee to update. Please provide their name, email, or ID.",
                ChatResponseType.CLARIFICATION,
                null,
                ctx);
        }

        try
        {
            var updateDto = new UpdateEmployeeDto
            {
                FullName = e.FullName ?? employee.FullName,
                Email = e.Email ?? employee.Email,
                Department = e.Department ?? employee.Department,
                Salary = e.Salary ?? employee.Salary
            };

            _logger.LogDebug(
                "Updating employee {Id}: FullName={FullName}, Email={Email}, Department={Department}, Salary={Salary}",
                employee.Id, updateDto.FullName, updateDto.Email, updateDto.Department, updateDto.Salary);

            var updated = await _employeeService.UpdateAsync(employee.Id, updateDto);

            _logger.LogInformation("Employee updated via chat. Id={Id}", updated.Id);
            ctx["lastViewedEmployeeId"] = updated.Id;
            
            return BuildResponse(
                $"✅ {updated.FullName} has been successfully updated.",
                ChatResponseType.SUCCESS,
                new { employee = updated, needsRefresh = true },
                ctx);
        }
        catch (ValidationException vex)
        {
            var errors = vex.Errors.Select(x => x.ErrorMessage).ToList();
            _logger.LogWarning("Update validation failed: {Errors}", string.Join("; ", errors));
            return BuildErrorResponse($"Update failed: {string.Join(", ", errors)}", ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed for employee {Id}", employee.Id);
            return BuildErrorResponse($"Failed to update {employee.FullName}: {ex.Message}", ctx);
        }
    }

    private async Task<ChatResponse> HandleDeleteAsync(
        string userMessage, AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        // Check whether user is confirming a pending delete
        var isConfirm = userMessage.Contains("confirm", StringComparison.OrdinalIgnoreCase)
                     || userMessage.Contains("yes", StringComparison.OrdinalIgnoreCase)
                     || userMessage.Contains("proceed", StringComparison.OrdinalIgnoreCase);

        if (isConfirm && ctx.TryGetValue("pendingDeleteEmployeeId", out var pendingIdObj) && pendingIdObj is int or long)
        {
            var deleteId = Convert.ToInt32(pendingIdObj);
            try
            {
                var toDelete = await _employeeService.GetByIdAsync(deleteId);
                await _employeeService.DeleteAsync(toDelete.Id);
                ctx.Remove("pendingDeleteEmployeeId");
                _logger.LogInformation("Employee deleted via chat. Id={Id}, Name={Name}", toDelete.Id, toDelete.FullName);
                
                return BuildResponse(
                    $"✅ {toDelete.FullName} has been successfully deleted from the system.",
                    ChatResponseType.SUCCESS,
                    new { deletedEmployee = toDelete, needsRefresh = true },
                    ctx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete employee {Id}", deleteId);
                return BuildErrorResponse($"Failed to delete employee: {ex.Message}", ctx);
            }
        }

        // First request — find the employee and ask for confirmation
        var e = ai.Entities;
        EmployeeDto? employee = null;

        _logger.LogDebug(
            "HandleDeleteAsync: EmployeeId={EmployeeId}, FullName={FullName}, Email={Email}",
            e.EmployeeId, e.FullName, e.Email);

        // Try by ID first
        if (e.EmployeeId.HasValue)
        {
            try
            {
                employee = await _employeeService.GetByIdAsync(e.EmployeeId.Value);
                _logger.LogDebug("Found employee by ID: {Id} - {Name}", employee.Id, employee.FullName);
            }
            catch (NotFoundException)
            {
                _logger.LogDebug("Employee not found by ID: {Id}", e.EmployeeId.Value);
            }
        }

        // Try by name or email
        if (employee == null)
        {
            var search = e.FullName ?? e.Email;
            if (!string.IsNullOrWhiteSpace(search))
            {
                _logger.LogDebug("Searching for employee by: {Search}", search);
                var results = await _employeeService.GetAllAsync(1, 50, search, null);
                _logger.LogDebug("Search returned {Count} results", results.Data.Count);
                
                if (results.Data.Count == 1)
                {
                    employee = results.Data[0];
                    _logger.LogDebug("Found exact match: {Id} - {Name}", employee.Id, employee.FullName);
                }
                else if (results.Data.Count > 1)
                {
                    _logger.LogDebug("Multiple matches found, need clarification");
                    return BuildResponse(
                        $"I found {results.Data.Count} employees matching '{search}'. Which one would you like to delete?\n" +
                        string.Join("\n", results.Data.Select(emp => $"• {emp.FullName} ({emp.Email}) - ID: {emp.Id}")),
                        ChatResponseType.CLARIFICATION,
                        new { matches = results.Data },
                        ctx);
                }
            }
        }

        // Try from last viewed/created employee in context (by ID)
        if (employee == null && ctx.TryGetValue("lastCreatedEmployeeId", out var lastCreatedIdObj) && lastCreatedIdObj is int or long)
        {
            var createdId = Convert.ToInt32(lastCreatedIdObj);
            try
            {
                employee = await _employeeService.GetByIdAsync(createdId);
                _logger.LogDebug("Using lastCreatedEmployeeId from context: {Id}", createdId);
            }
            catch (NotFoundException)
            {
                _logger.LogDebug("lastCreatedEmployeeId {Id} no longer exists", createdId);
            }
        }

        if (employee == null && ctx.TryGetValue("lastViewedEmployeeId", out var lastViewedIdObj) && lastViewedIdObj is int or long)
        {
            var viewedId = Convert.ToInt32(lastViewedIdObj);
            try
            {
                employee = await _employeeService.GetByIdAsync(viewedId);
                _logger.LogDebug("Using lastViewedEmployeeId from context: {Id}", viewedId);
            }
            catch (NotFoundException)
            {
                _logger.LogDebug("lastViewedEmployeeId {Id} no longer exists", viewedId);
            }
        }

        if (employee == null)
        {
            _logger.LogWarning("Could not identify employee for delete");
            return BuildResponse(
                "I couldn't identify which employee to delete. Please provide their name, email, or ID.",
                ChatResponseType.CLARIFICATION,
                null,
                ctx);
        }

        ctx["pendingDeleteEmployeeId"] = employee.Id;
        
        _logger.LogInformation("Storing pendingDeleteEmployeeId={Id} in context for confirmation", employee.Id);
        
        return BuildResponse(
            $"⚠️ Are you sure you want to delete {employee.FullName} ({employee.Email})? Reply 'yes' or 'confirm' to proceed.",
            ChatResponseType.CONFIRMATION,
            new { employee },
            ctx);
    }

    private async Task<ChatResponse> HandleSearchAsync(AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        var e = ai.Entities;
        var results = await _employeeService.GetAllAsync(1, 100, e.SearchQuery, e.Department);

        if (results.Data.Count == 0)
            return BuildResponse(ai.ResponseText, ChatResponseType.INFO, null, ctx);

        return BuildResponse(ai.ResponseText, ChatResponseType.SUCCESS,
            new { employees = results.Data, totalCount = results.Data.Count }, ctx);
    }

    // ── Bulk Handlers ─────────────────────────────────────────────────────────

    private async Task<ChatResponse> HandleBulkCreateAsync(
        string userMessage, AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        // 1. Get rows either from AI-extracted entities or by parsing the message directly
        var rows = ai.Entities.BulkEmployees ?? _tableParser.TryParse(userMessage);

        if (rows == null || rows.Count == 0)
            return BuildResponse(
                "I see you're trying to add multiple employees, but I couldn't parse the data. " +
                "Please use a table format with columns: Full Name | Email | Department | Salary",
                ChatResponseType.CLARIFICATION, null, ctx);

        // 2. First turn — present preview and ask for confirmation
        if (!ctx.TryGetValue("bulkCreateConfirmed", out _))
        {
            ctx["pendingBulkCreate"] = rows;
            var preview = rows.Select((r, i) => new
            {
                row = i + 1,
                r.FullName,
                r.Email,
                r.Department,
                r.Salary,
                isValid = !string.IsNullOrWhiteSpace(r.FullName)
                       && !string.IsNullOrWhiteSpace(r.Email)
                       && !string.IsNullOrWhiteSpace(r.Department)
                       && r.Salary.HasValue && r.Salary > 0
            }).ToList();

            int validCount = preview.Count(p => p.isValid);
            int invalidCount = preview.Count - validCount;

            var previewText = invalidCount > 0
                ? $"I found {rows.Count} employee records. {validCount} are ready to create and {invalidCount} have missing or invalid data. Would you like me to proceed with the valid records? (Reply 'confirm' to proceed)"
                : $"I found {rows.Count} employee records. All records look valid. Would you like me to create all of them? (Reply 'confirm' to proceed)";

            return BuildResponse(previewText, ChatResponseType.CONFIRMATION, new { preview, validCount, invalidCount }, ctx);
        }

        // 3. Confirmed — execute
        return await ExecuteBulkCreateAsync(ai, ctx, ct);
    }

    private async Task<ChatResponse> HandleBulkUpdateAsync(
        string userMessage, AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        // Extract list of employees to update (from AI or search)
        var rows = ai.Entities.BulkEmployees;

        if (rows == null || rows.Count == 0)
        {
            // Try to parse from department or search filter
            var dept = ai.Entities.Department;
            if (!string.IsNullOrWhiteSpace(dept))
            {
                return BuildResponse(
                    $"⚠️ Bulk update by department '{dept}' requires confirmation. " +
                    "Please confirm: 'Update all employees in {dept} department'",
                    ChatResponseType.CLARIFICATION,
                    null,
                    ctx);
            }

            return BuildResponse(
                "I need a list of employees or criteria to update. " +
                "Example: 'Update all employees in IT department to set salary 80000'",
                ChatResponseType.CLARIFICATION,
                null,
                ctx);
        }

        // First turn — ask for confirmation
        if (!ctx.TryGetValue("bulkUpdateConfirmed", out _))
        {
            ctx["pendingBulkUpdate"] = rows;
            return BuildResponse(
                $"⚠️ You're about to update {rows.Count} employees. Reply 'confirm' to proceed.",
                ChatResponseType.CONFIRMATION,
                new { preview = rows },
                ctx);
        }

        // Confirmed — execute
        return await ExecuteBulkUpdateAsync(ai, ctx, ct);
    }

    private async Task<ChatResponse> ExecuteBulkUpdateAsync(
        AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        if (!ctx.TryGetValue("pendingBulkUpdate", out var pendingObj) || pendingObj is not List<BulkEmployeeData> rows)
            return BuildErrorResponse("I lost track of the pending updates. Please try again.", ctx);

        ctx.Remove("pendingBulkUpdate");
        ctx.Remove("bulkUpdateConfirmed");

        var results = new List<object>();
        int successCount = 0, failCount = 0;

        foreach (var (row, i) in rows.Select((r, i) => (r, i + 1)))
        {
            try
            {
                // Find employee by name or email
                var search = row.FullName ?? row.Email;
                if (string.IsNullOrWhiteSpace(search))
                {
                    results.Add(new { row = i, success = false, error = "Missing employee identifier" });
                    failCount++;
                    continue;
                }

                var searchResults = await _employeeService.GetAllAsync(1, 10, search, null);
                if (searchResults.Data.Count != 1)
                {
                    results.Add(new { row = i, success = false, error = $"Found {searchResults.Data.Count} matches for '{search}'" });
                    failCount++;
                    continue;
                }

                var employee = searchResults.Data[0];
                var updateDto = new UpdateEmployeeDto
                {
                    FullName = row.FullName ?? employee.FullName,
                    Email = row.Email ?? employee.Email,
                    Department = row.Department ?? employee.Department,
                    Salary = row.Salary ?? employee.Salary
                };

                var updated = await _employeeService.UpdateAsync(employee.Id, updateDto);
                results.Add(new { row = i, success = true, employee = updated });
                successCount++;
            }
            catch (Exception ex)
            {
                results.Add(new { row = i, success = false, error = ex.Message });
                failCount++;
                _logger.LogWarning(ex, "Bulk update row {Row} failed.", i);
            }
        }

        _logger.LogInformation("Bulk update completed. Success={S}, Fail={F}", successCount, failCount);

        var summary = failCount == 0
            ? $"✅ All {successCount} employees have been updated successfully!"
            : $"Bulk update completed: {successCount} updated, {failCount} failed. See details below.";

        return BuildResponse(
            summary,
            successCount > 0 ? ChatResponseType.SUCCESS : ChatResponseType.ERROR,
            new { results, successCount, failCount, totalRows = rows.Count, needsRefresh = successCount > 0 },
            ctx);
    }

    private async Task<ChatResponse> HandleBulkDeleteAsync(
        string userMessage, AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        // Check if user is confirming
        var isConfirm = userMessage.Contains("confirm", StringComparison.OrdinalIgnoreCase)
                     || userMessage.Contains("yes", StringComparison.OrdinalIgnoreCase)
                     || userMessage.Contains("proceed", StringComparison.OrdinalIgnoreCase);

        if (isConfirm && ctx.TryGetValue("pendingBulkDelete", out var pending) && pending is List<EmployeeDto> toDelete)
        {
            return await ExecuteBulkDeleteAsync(toDelete, ctx, ct);
        }

        // Extract employees to delete (by department, list, etc.)
        var dept = ai.Entities.Department;
        var searchQuery = ai.Entities.SearchQuery;

        List<EmployeeDto> employees;

        if (!string.IsNullOrWhiteSpace(dept))
        {
            var results = await _employeeService.GetAllAsync(1, 100, null, dept);
            employees = results.Data;
        }
        else if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var results = await _employeeService.GetAllAsync(1, 100, searchQuery, null);
            employees = results.Data;
        }
        else
        {
            return BuildResponse(
                "I need criteria for bulk delete. Example: 'Delete all employees in IT department'",
                ChatResponseType.CLARIFICATION,
                null,
                ctx);
        }

        if (employees.Count == 0)
        {
            return BuildErrorResponse("No employees found matching your criteria.", ctx);
        }

        // Ask for confirmation
        ctx["pendingBulkDelete"] = employees;
        return BuildResponse(
            $"⚠️ **WARNING**: You're about to delete {employees.Count} employees:\n" +
            string.Join("\n", employees.Take(10).Select(e => $"• {e.FullName} ({e.Email})")) +
            (employees.Count > 10 ? $"\n... and {employees.Count - 10} more" : "") +
            "\n\nReply 'confirm' to proceed or anything else to cancel.",
            ChatResponseType.CONFIRMATION,
            new { employees },
            ctx);
    }

    private async Task<ChatResponse> ExecuteBulkDeleteAsync(
        List<EmployeeDto> employees, Dictionary<string, object> ctx, CancellationToken ct)
    {
        ctx.Remove("pendingBulkDelete");

        var results = new List<object>();
        int successCount = 0, failCount = 0;

        foreach (var (emp, i) in employees.Select((e, i) => (e, i + 1)))
        {
            try
            {
                await _employeeService.DeleteAsync(emp.Id);
                results.Add(new { row = i, success = true, employee = emp });
                successCount++;
            }
            catch (Exception ex)
            {
                results.Add(new { row = i, success = false, error = ex.Message, employee = emp });
                failCount++;
                _logger.LogWarning(ex, "Bulk delete failed for employee {Id}", emp.Id);
            }
        }

        _logger.LogInformation("Bulk delete completed. Success={S}, Fail={F}", successCount, failCount);

        var summary = failCount == 0
            ? $"✅ All {successCount} employees have been deleted successfully!"
            : $"Bulk delete completed: {successCount} deleted, {failCount} failed. See details below.";

        return BuildResponse(
            summary,
            successCount > 0 ? ChatResponseType.SUCCESS : ChatResponseType.ERROR,
            new { results, successCount, failCount, totalRows = employees.Count, needsRefresh = successCount > 0 },
            ctx);
    }

    private async Task<ChatResponse> HandleConfirmationAsync(
        string userMessage, AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        // Route to the right pending operation
        if (ctx.ContainsKey("pendingDeleteEmployeeId"))
            return await HandleDeleteAsync(userMessage, ai, ctx, ct);

        if (ctx.ContainsKey("pendingBulkCreate"))
        {
            ctx["bulkCreateConfirmed"] = true;
            return await HandleBulkCreateAsync(userMessage, ai, ctx, ct);
        }

        if (ctx.ContainsKey("pendingBulkUpdate"))
        {
            ctx["bulkUpdateConfirmed"] = true;
            return await HandleBulkUpdateAsync(userMessage, ai, ctx, ct);
        }

        if (ctx.ContainsKey("pendingBulkDelete"))
            return await HandleBulkDeleteAsync(userMessage, ai, ctx, ct);

        return BuildResponse(ai.ResponseText, ChatResponseType.INFO, null, ctx);
    }

    private async Task<ChatResponse> ExecuteBulkCreateAsync(
        AIResponse ai, Dictionary<string, object> ctx, CancellationToken ct)
    {
        if (!ctx.TryGetValue("pendingBulkCreate", out var pendingObj) || pendingObj is not List<BulkEmployeeData> rows)
            return BuildErrorResponse("I lost track of the pending records. Please paste the table again.", ctx);

        ctx.Remove("pendingBulkCreate");
        ctx.Remove("bulkCreateConfirmed");

        var results = new List<object>();
        int successCount = 0, failCount = 0;

        foreach (var (row, i) in rows.Select((r, i) => (r, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(row.FullName) || string.IsNullOrWhiteSpace(row.Email) ||
                string.IsNullOrWhiteSpace(row.Department) || !row.Salary.HasValue || row.Salary <= 0)
            {
                results.Add(new { row = i, success = false, error = "Missing required fields." });
                failCount++;
                continue;
            }

            try
            {
                var dto = new CreateEmployeeDto
                {
                    FullName = row.FullName,
                    Email = row.Email,
                    Department = row.Department,
                    Salary = row.Salary.Value
                };
                var created = await _employeeService.CreateAsync(dto);
                results.Add(new { row = i, success = true, employee = created });
                successCount++;
            }
            catch (ValidationException vex)
            {
                var errors = vex.Errors.Select(x => x.ErrorMessage).ToList();
                results.Add(new { row = i, success = false, error = string.Join(", ", errors) });
                failCount++;
            }
            catch (Exception ex)
            {
                results.Add(new { row = i, success = false, error = ex.Message });
                failCount++;
                _logger.LogWarning(ex, "Bulk create row {Row} failed.", i);
            }
        }

        _logger.LogInformation("Bulk create completed. Success={S}, Fail={F}", successCount, failCount);

        var summary = failCount == 0
            ? $"✅ All {successCount} employees have been created successfully!"
            : $"Bulk creation completed: {successCount} created, {failCount} failed. See details below.";

        return BuildResponse(
            summary,
            successCount > 0 ? ChatResponseType.SUCCESS : ChatResponseType.ERROR,
            new { results, successCount, failCount, totalRows = rows.Count, needsRefresh = successCount > 0 },
            ctx);
    }

    // ── Context Builder ───────────────────────────────────────────────────────

    private async Task<List<EmployeeContextItem>> GetRelevantContextAsync(string userMessage)
    {
        try
        {
            var result = await _employeeService.GetAllAsync(1, MaxContextEmployees);
            return result.Data.Select(e => new EmployeeContextItem
            {
                Id = e.Id,
                FullName = e.FullName,
                Email = e.Email,
                Department = e.Department
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load employee context for AI request.");
            return new List<EmployeeContextItem>();
        }
    }

    // ── Response Builders ─────────────────────────────────────────────────────

    private static ChatResponse BuildResponse(
        string text, ChatResponseType type, object? data = null, Dictionary<string, object>? ctx = null) =>
        new()
        {
            ResponseText = text,
            Type = type,
            Data = data,
            UpdatedContext = ctx ?? new Dictionary<string, object>()
        };

    private static ChatResponse BuildErrorResponse(string message, Dictionary<string, object>? ctx = null) =>
        BuildResponse(message, ChatResponseType.ERROR, null, ctx);

    private static ChatResponse BuildHelpResponse(Dictionary<string, object> ctx) =>
        BuildResponse(
            "I'm your AI Employee Management Assistant! Here's what I can do:\n\n" +
            "• **Create employees** — \"Add John Doe, john@example.com, Engineering, $85,000\"\n" +
            "• **Find employees** — \"Who is John Doe?\" or \"Find employee 5\"\n" +
            "• **List all employees** — \"Show me everyone\" or \"List all employees\"\n" +
            "• **Update employees** — \"Change John's salary to $90,000\"\n" +
            "• **Delete employees** — \"Remove John Doe\"\n" +
            "• **Search** — \"Who works in IT?\"\n" +
            "• **Bulk add** — Paste a table with Name, Email, Department, Salary columns\n\n" +
            "Just talk to me naturally — I understand many different ways of asking!",
            ChatResponseType.INFO, null, ctx);
}
