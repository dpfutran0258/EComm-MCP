# Gemini AI Integration Fix - Design Document

## Overview

The Gemini AI chatbot integration is currently failing because it expects structured JSON responses from the Gemini API but receives plain text instead. This design document outlines a comprehensive fix that leverages Gemini's native JSON schema mode, proper API parameter names, structured error handling, and thread-safe conversation management.

The fix focuses on:
1. Using Gemini's JSON schema validation to guarantee structured responses
2. Correcting API parameter names (`response_mime_type` instead of `responseMimeType`)
3. Implementing structured ChatContext instead of loosely-typed Dictionary
4. Adding thread-safe conversation history with ConcurrentDictionary
5. Comprehensive error categorization and handling
6. Proper use of `systemInstruction` in Gemini API v1beta

This approach ensures the chatbot can reliably interpret user intents, extract entities, and provide meaningful responses while maintaining backward compatibility with existing functionality.

## Glossary

- **Bug_Condition (C)**: The condition that triggers parsing failures - when Gemini returns plain text instead of structured JSON because JSON mode is not properly configured
- **Property (P)**: The desired behavior - Gemini returns valid JSON that successfully parses into GeminiResponse structure
- **Preservation**: Existing functionality that must remain unchanged - caching, conversation history length limits, health checks, and employee service interactions
- **GeminiService**: The service class in `EmployeeManagement.Application/Services/AI/GeminiService.cs` that interfaces with Google Gemini API
- **ParseGeminiResponse**: The method that extracts structured data from Gemini API responses
- **JSON Schema Mode**: Gemini's native capability to enforce structured JSON responses using a predefined schema
- **systemInstruction**: The proper parameter in Gemini API v1beta for providing system-level instructions (replaces inline system prompts)
- **response_mime_type**: The correct snake_case parameter name for specifying JSON response format in Gemini API
- **ChatContext**: A structured DTO that replaces Dictionary<string, object> for passing typed context between layers
- **ConcurrentDictionary**: Thread-safe dictionary implementation for managing conversation history across concurrent requests

## Bug Details

### Bug Condition

The bug manifests when `GeminiService.ProcessChatMessageAsync()` attempts to parse responses from the Gemini API. The parsing fails because Gemini returns natural language text instead of structured JSON, causing `JsonDocument.Parse()` to throw exceptions.

**Formal Specification:**
```
FUNCTION isBugCondition(apiRequest, apiResponse)
  INPUT: apiRequest of type GeminiApiRequest
         apiResponse of type GeminiApiResponse
  OUTPUT: boolean
  
  RETURN (NOT apiRequest.generationConfig.contains("response_mime_type"))
         OR (apiRequest.generationConfig["response_mime_type"] != "application/json")
         OR (NOT apiRequest.generationConfig.contains("response_schema"))
         OR (apiResponse.candidates[0].content.parts[0].text is not valid JSON)
         OR (ParseGeminiResponse throws JsonException)
END FUNCTION
```

**Root Causes:**
1. **Missing JSON Mode Configuration**: The `responseMimeType` parameter was removed without replacement, so Gemini defaults to plain text responses
2. **Wrong Parameter Name**: Using camelCase `responseMimeType` instead of snake_case `response_mime_type` that Gemini API expects
3. **No Response Schema**: Not providing a JSON schema to validate Gemini's output structure
4. **Inadequate Error Handling**: Generic catch blocks mask specific API errors (quota exceeded, blocked content, invalid keys)
5. **Type-Unsafe Context Passing**: Using `Dictionary<string, object>` for context makes type validation impossible
6. **Race Conditions**: Non-thread-safe Dictionary for conversation history can corrupt data under concurrent access
7. **Improper System Prompting**: System instructions are embedded in user prompt instead of using dedicated `systemInstruction` parameter

### Examples

**Example 1: User sends "add yusuf"**
- Current: API returns plain text → JsonException → fallback "I'm having trouble processing your request"
- Expected: API returns `{"intent": "CREATE_EMPLOYEE", "confidence": 0.4, "responseText": "I need more information...", "entities": {"fullName": "yusuf"}}`

**Example 2: User sends "hello"**
- Current: API returns "Hello! How can I help you today?" → JsonException → fallback with confidence 0.3
- Expected: API returns `{"intent": "GREETING", "confidence": 0.95, "responseText": "Hello! How can I help you today?", "entities": {}}`

**Example 3: Gemini API quota exceeded**
- Current: HTTP 429 error → logged as "HTTP error calling Gemini API" → generic "I'm having trouble..." response
- Expected: Specific error caught → logged with quota details → user-friendly "Service temporarily unavailable due to rate limit" message

**Example 4: Concurrent requests to same session**
- Current: Two requests modify conversation history simultaneously → potential data corruption or lost messages
- Expected: ConcurrentDictionary ensures thread-safe access → all messages preserved correctly

## Expected Behavior

### JSON Schema and Structured Responses

The system will use Gemini's JSON mode with a predefined schema to guarantee structured responses:

**JSON Schema Structure:**
```json
{
  "type": "object",
  "properties": {
    "intent": {
      "type": "string",
      "enum": [
        "UNKNOWN", "GREETING", "HELP", "CREATE_EMPLOYEE",
        "READ_EMPLOYEE", "READ_ALL_EMPLOYEES", "UPDATE_EMPLOYEE",
        "DELETE_EMPLOYEE", "SEARCH_EMPLOYEES", "CALCULATE_STATS",
        "CLARIFICATION_NEEDED"
      ]
    },
    "confidence": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0
    },
    "responseText": {
      "type": "string"
    },
    "entities": {
      "type": "object",
      "properties": {
        "employeeId": {"type": ["integer", "null"]},
        "fullName": {"type": ["string", "null"]},
        "email": {"type": ["string", "null"]},
        "department": {"type": ["string", "null"]},
        "salary": {"type": ["number", "null"]},
        "searchQuery": {"type": ["string", "null"]}
      }
    },
    "suggestions": {
      "type": "array",
      "items": {"type": "string"}
    }
  },
  "required": ["intent", "confidence", "responseText", "entities"]
}
```

This schema will be sent in the API request to enforce structure validation on Gemini's side.

### Gemini API Request Structure

The corrected API request structure using proper parameter names and systemInstruction:

```csharp
var requestBody = new
{
    system_instruction = new
    {
        parts = new[] 
        { 
            new { text = systemPrompt } 
        }
    },
    contents = new[]
    {
        new
        {
            role = "user",
            parts = new[]
            {
                new { text = BuildUserPromptWithHistory(conversationMessages, userMessage) }
            }
        }
    },
    generationConfig = new
    {
        temperature = _config.Temperature,
        topK = _config.TopK,
        topP = _config.TopP,
        maxOutputTokens = _config.MaxOutputTokens,
        response_mime_type = "application/json",  // snake_case, not camelCase
        response_schema = jsonSchema  // Schema object as shown above
    }
};
```

**Key Changes:**
- `system_instruction` at root level (not in contents)
- `response_mime_type` using snake_case
- `response_schema` with full JSON schema validation
- Conversation history in user message context, not as separate content entries

### ChatContext DTO Design

Replace loosely-typed `Dictionary<string, object>` with structured DTO:

```csharp
public class ChatContext
{
    public string SessionId { get; set; } = string.Empty;
    public int? LastCreatedEmployeeId { get; set; }
    public int? PendingDeleteEmployeeId { get; set; }
    public string? PendingDeleteEmployeeName { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    
    // Factory method for backward compatibility
    public static ChatContext FromDictionary(Dictionary<string, object> dict, string sessionId)
    {
        var context = new ChatContext { SessionId = sessionId };
        
        if (dict.TryGetValue("lastCreatedEmployee", out var lastCreated) 
            && lastCreated is EmployeeDto emp)
        {
            context.LastCreatedEmployeeId = emp.Id;
        }
        
        if (dict.TryGetValue("pendingDeleteEmployee", out var pendingDelete) 
            && pendingDelete is EmployeeDto delEmp)
        {
            context.PendingDeleteEmployeeId = delEmp.Id;
            context.PendingDeleteEmployeeName = delEmp.FullName;
        }
        
        context.AdditionalData = new Dictionary<string, object>(dict);
        return context;
    }
    
    // For backward compatibility with existing code
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>(AdditionalData);
        
        if (LastCreatedEmployeeId.HasValue)
            dict["lastCreatedEmployeeId"] = LastCreatedEmployeeId.Value;
            
        if (PendingDeleteEmployeeId.HasValue)
        {
            dict["pendingDeleteEmployeeId"] = PendingDeleteEmployeeId.Value;
            dict["pendingDeleteEmployeeName"] = PendingDeleteEmployeeName ?? string.Empty;
        }
        
        return dict;
    }
}
```

**Benefits:**
- Type-safe property access
- Clear documentation of expected context fields
- Backward compatibility with existing Dictionary usage
- Extensible through AdditionalData for future needs

### Thread-Safe Conversation History

Replace `Dictionary<string, List<ConversationMessage>>` with `ConcurrentDictionary`:

```csharp
private readonly ConcurrentDictionary<string, List<ConversationMessage>> _conversationHistory = new();

private void UpdateConversationHistory(string sessionId, string userMessage, string assistantResponse)
{
    var messages = _conversationHistory.GetOrAdd(sessionId, _ => new List<ConversationMessage>());
    
    lock (messages)  // Lock on the list itself for mutation safety
    {
        messages.Add(new ConversationMessage
        {
            Role = "user",
            Content = userMessage,
            Timestamp = DateTime.UtcNow
        });

        messages.Add(new ConversationMessage
        {
            Role = "assistant",
            Content = assistantResponse,
            Timestamp = DateTime.UtcNow
        });

        // Trim history if too long
        if (messages.Count > _config.MaxHistoryLength * 2)
        {
            messages.RemoveRange(0, messages.Count - (_config.MaxHistoryLength * 2));
        }
    }
}

private List<ConversationMessage> GetConversationMessages(string sessionId)
{
    if (_conversationHistory.TryGetValue(sessionId, out var messages))
    {
        lock (messages)
        {
            return messages.TakeLast(_config.MaxHistoryLength * 2).ToList();
        }
    }
    return new List<ConversationMessage>();
}
```

**Benefits:**
- Thread-safe dictionary operations
- Lock-based list mutation protection
- No race conditions on concurrent session access
- Maintains existing history length limits

### Error Handling Strategy

Implement categorized error handling with specific recovery strategies:

```csharp
public enum GeminiErrorCategory
{
    InvalidApiKey,           // 401 - API key missing or invalid
    QuotaExceeded,          // 429 - Rate limit or quota exceeded
    InvalidRequest,         // 400 - Malformed request
    ContentBlocked,         // Response blocked by safety filters
    ServiceUnavailable,     // 503 - Gemini service down
    Timeout,                // Request timeout
    ParsingError,           // Response parsing failure
    UnknownError            // Catch-all
}

private GeminiResponse HandleGeminiError(Exception ex, string userMessage, GeminiErrorCategory category)
{
    var errorMessages = new Dictionary<GeminiErrorCategory, string>
    {
        [GeminiErrorCategory.InvalidApiKey] = "AI service configuration error. Please check API key.",
        [GeminiErrorCategory.QuotaExceeded] = "AI service temporarily unavailable due to rate limits. Please try again in a moment.",
        [GeminiErrorCategory.InvalidRequest] = "Unable to process request format. Please try rephrasing.",
        [GeminiErrorCategory.ContentBlocked] = "Unable to process this request due to content filters.",
        [GeminiErrorCategory.ServiceUnavailable] = "AI service temporarily unavailable. Please try again later.",
        [GeminiErrorCategory.Timeout] = "Request timed out. Please try a simpler query.",
        [GeminiErrorCategory.ParsingError] = "Unable to understand AI response. Please try again.",
        [GeminiErrorCategory.UnknownError] = "An unexpected error occurred. Please try again."
    };

    _logger.LogError(ex, "Gemini Error [{Category}]: {Message}", category, ex.Message);

    return new GeminiResponse
    {
        ResponseText = errorMessages[category],
        Intent = ChatIntent.UNKNOWN,
        Confidence = 0,
        Metadata = new Dictionary<string, object> 
        { 
            { "errorCategory", category.ToString() },
            { "errorMessage", ex.Message },
            { "userMessage", userMessage }
        }
    };
}

private GeminiErrorCategory CategorizeHttpError(HttpRequestException ex)
{
    if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        return GeminiErrorCategory.InvalidApiKey;
    if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        return GeminiErrorCategory.QuotaExceeded;
    if (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        return GeminiErrorCategory.InvalidRequest;
    if (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        return GeminiErrorCategory.ServiceUnavailable;
    
    return GeminiErrorCategory.UnknownError;
}
```

**Error Flow:**
1. HTTP errors → Categorized by status code → Specific logging and user message
2. Timeout exceptions → Timeout category → Suggest simpler query
3. JSON parsing errors → ParsingError category → Log raw response, return safe fallback
4. Empty candidates → ContentBlocked category → Indicate safety filter issue
5. Unknown errors → Generic handling with full exception logging

### Response Parsing with Fallback Strategies

Enhanced parsing with multiple fallback layers:

```csharp
private GeminiResponse ParseGeminiResponse(string apiResponse, string originalMessage)
{
    try
    {
        var jsonDoc = JsonDocument.Parse(apiResponse);
        var root = jsonDoc.RootElement;

        // Check for blocked content
        if (root.TryGetProperty("candidates", out var candidates))
        {
            if (candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini returned empty candidates - likely blocked by safety filters");
                return HandleGeminiError(
                    new InvalidOperationException("Content blocked by safety filters"),
                    originalMessage,
                    GeminiErrorCategory.ContentBlocked);
            }

            var firstCandidate = candidates[0];
            
            // Check for finish reason
            if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
            {
                var reason = finishReason.GetString();
                if (reason == "SAFETY" || reason == "RECITATION" || reason == "OTHER")
                {
                    _logger.LogWarning("Gemini blocked response with reason: {Reason}", reason);
                    return HandleGeminiError(
                        new InvalidOperationException($"Response blocked: {reason}"),
                        originalMessage,
                        GeminiErrorCategory.ContentBlocked);
                }
            }

            // Extract generated text
            if (firstCandidate.TryGetProperty("content", out var content))
            {
                if (content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var text))
                    {
                        var generatedText = text.GetString() ?? string.Empty;
                        
                        if (string.IsNullOrWhiteSpace(generatedText))
                        {
                            throw new InvalidOperationException("Empty text generated by Gemini");
                        }

                        // Parse the structured JSON response
                        return ParseStructuredResponse(generatedText, originalMessage);
                    }
                }
            }
        }

        throw new InvalidOperationException("Invalid Gemini API response structure");
    }
    catch (JsonException jsonEx)
    {
        _logger.LogError(jsonEx, "JSON parsing error. Raw response: {Response}", apiResponse);
        return HandleGeminiError(jsonEx, originalMessage, GeminiErrorCategory.ParsingError);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error parsing Gemini response");
        return HandleGeminiError(ex, originalMessage, GeminiErrorCategory.UnknownError);
    }
}

private GeminiResponse ParseStructuredResponse(string jsonText, string originalMessage)
{
    try
    {
        var geminiJson = JsonDocument.Parse(jsonText);
        var geminiRoot = geminiJson.RootElement;

        // Validate required fields exist
        if (!geminiRoot.TryGetProperty("intent", out var intentProp) ||
            !geminiRoot.TryGetProperty("confidence", out var confidenceProp) ||
            !geminiRoot.TryGetProperty("responseText", out var textProp))
        {
            throw new InvalidOperationException("Missing required fields in structured response");
        }

        var response = new GeminiResponse
        {
            ResponseText = textProp.GetString() ?? "I'm not sure how to respond to that.",
            Intent = Enum.Parse<ChatIntent>(intentProp.GetString() ?? "UNKNOWN", ignoreCase: true),
            Confidence = (decimal)confidenceProp.GetDouble(),
        };

        // Parse entities with null safety
        if (geminiRoot.TryGetProperty("entities", out var entities))
        {
            response.Entities = ParseEntities(entities);
        }

        // Parse suggestions
        if (geminiRoot.TryGetProperty("suggestions", out var suggestions) 
            && suggestions.ValueKind == JsonValueKind.Array)
        {
            response.Suggestions = suggestions.EnumerateArray()
                .Where(s => s.ValueKind == JsonValueKind.String)
                .Select(s => s.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        return response;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error parsing structured response. JSON: {Json}", jsonText);
        
        // Final fallback - return basic response
        return new GeminiResponse
        {
            ResponseText = "I understood your message but couldn't form a proper response. Please try again.",
            Intent = ChatIntent.UNKNOWN,
            Confidence = 0.1m,
            Entities = new ExtractedEntities(),
            Suggestions = new List<string> 
            { 
                "Try asking me to list all employees", 
                "Ask me to add a new employee with full details" 
            }
        };
    }
}

private ExtractedEntities ParseEntities(JsonElement entities)
{
    return new ExtractedEntities
    {
        EmployeeId = TryGetInt(entities, "employeeId"),
        FullName = TryGetString(entities, "fullName"),
        Email = TryGetString(entities, "email"),
        Department = TryGetString(entities, "department"),
        Salary = TryGetDecimal(entities, "salary"),
        SearchQuery = TryGetString(entities, "searchQuery")
    };
}

private int? TryGetInt(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var prop) 
           && prop.ValueKind == JsonValueKind.Number 
        ? prop.GetInt32() 
        : null;
}

private string? TryGetString(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var prop) 
           && prop.ValueKind == JsonValueKind.String 
        ? prop.GetString() 
        : null;
}

private decimal? TryGetDecimal(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var prop) 
           && prop.ValueKind == JsonValueKind.Number 
        ? (decimal)prop.GetDouble() 
        : null;
}
```

**Fallback Strategy:**
1. Check for empty candidates → Content blocked error
2. Check finish reason → Detect safety/recitation blocks
3. Extract text from response structure
4. Parse structured JSON with required field validation
5. Validate and parse optional fields with null safety
6. Final fallback: Return minimal valid response

### Configuration Validation

Add validation on service initialization:

```csharp
public GeminiService(
    GeminiConfiguration config,
    ILogger<GeminiService> logger,
    IDistributedCache? cache = null,
    HttpClient? httpClient = null)
{
    _config = config ?? throw new ArgumentNullException(nameof(config));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _cache = cache;
    _httpClient = httpClient ?? new HttpClient();
    _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

    ValidateConfiguration();
}

private void ValidateConfiguration()
{
    var errors = new List<string>();

    // API Key validation
    if (string.IsNullOrWhiteSpace(_config.ApiKey))
        errors.Add("Gemini API Key is required");
    else if (!_config.ApiKey.StartsWith("AIza"))
        errors.Add("Gemini API Key format appears invalid (should start with 'AIza')");

    // Model validation
    if (string.IsNullOrWhiteSpace(_config.Model))
        errors.Add("Gemini model name is required");
    else if (!_config.Model.StartsWith("gemini-"))
        errors.Add("Model name should start with 'gemini-'");

    // Numeric parameter validation
    if (_config.MaxOutputTokens <= 0 || _config.MaxOutputTokens > 8192)
        errors.Add("MaxOutputTokens must be between 1 and 8192");

    if (_config.Temperature < 0 || _config.Temperature > 2)
        errors.Add("Temperature must be between 0.0 and 2.0");

    if (_config.TopP < 0 || _config.TopP > 1)
        errors.Add("TopP must be between 0.0 and 1.0");

    if (_config.TopK < 1 || _config.TopK > 100)
        errors.Add("TopK must be between 1 and 100");

    if (_config.TimeoutSeconds < 1 || _config.TimeoutSeconds > 300)
        errors.Add("TimeoutSeconds must be between 1 and 300");

    if (_config.MaxHistoryLength < 1 || _config.MaxHistoryLength > 100)
        errors.Add("MaxHistoryLength must be between 1 and 100");

    if (_config.CacheExpirationMinutes < 1 || _config.CacheExpirationMinutes > 1440)
        errors.Add("CacheExpirationMinutes must be between 1 and 1440 (24 hours)");

    if (errors.Any())
    {
        var errorMessage = "Gemini configuration validation failed:\n" + string.Join("\n", errors);
        _logger.LogError("Configuration validation failed: {Errors}", string.Join("; ", errors));
        throw new InvalidOperationException(errorMessage);
    }

    _logger.LogInformation("Gemini configuration validated successfully");
}
```

## Preservation Requirements

### Unchanged Behaviors

The following functionality must remain exactly as implemented:

**Caching Behavior:**
- Response caching with Redis (when enabled) continues using existing cache key format
- Cache TTL respects CacheExpirationMinutes configuration
- Cache read/write failures are logged but don't block operations

**Conversation History:**
- History trimming at MaxHistoryLength * 2 messages
- History includes both user and assistant messages
- GetConversationHistoryAsync returns copies, not live references
- ClearConversationHistoryAsync removes entire session history

**Health Checks:**
- HealthCheckAsync sends test message and validates response
- Returns boolean (true for healthy, false for unhealthy)
- Does not throw exceptions

**Logging:**
- Detailed logging controlled by EnableDetailedLogging flag
- Request/response bodies logged only when detailed logging enabled
- API keys never logged (masked in output)
- Employee PII limited to what's necessary for debugging

**ChatService Integration:**
- Switch-based intent routing preserved
- All HandleX methods maintain existing signatures
- EmployeeService method calls unchanged
- Response type construction (SuccessResponse, ErrorResponse, etc.) unchanged

**HTTP Client:**
- Uses configured TimeoutSeconds
- HttpClient lifecycle managed by DI container
- EnsureSuccessStatusCode called for HTTP error detection

## Hypothesized Root Cause

Based on the bug description and code analysis, the root causes are:

1. **Missing JSON Schema Mode**: The `responseMimeType` parameter was removed from the generation config, causing Gemini to default to natural language text responses instead of structured JSON.

2. **Incorrect Parameter Naming**: The code likely used `responseMimeType` (camelCase) when Gemini API v1beta requires `response_mime_type` (snake_case). This parameter was silently ignored.

3. **No Response Schema Validation**: Without providing a JSON schema to Gemini, there's no guarantee of response structure even if JSON mode were enabled.

4. **Inadequate Error Context**: The catch block in `ProcessChatMessageAsync` masks underlying errors from `CallGeminiApiAsync` and `ParseGeminiResponse`, making diagnosis difficult.

5. **Type-Unsafe Context Passing**: Using `Dictionary<string, object>` between ChatController and ChatService creates potential for type mismatches and null reference errors.

6. **Non-Thread-Safe History**: Using standard Dictionary for conversation history allows race conditions when multiple requests for the same session arrive concurrently.

7. **Improper System Instruction Placement**: System prompts are embedded in the user message instead of using Gemini's dedicated `system_instruction` parameter, reducing effectiveness.

## Correctness Properties

Property 1: Bug Condition - Structured JSON Parsing Success

_For any_ valid user message processed through GeminiService, when Gemini API is called with proper JSON schema mode configuration (`response_mime_type: "application/json"` and valid `response_schema`), the API response SHALL contain valid JSON in the expected structure, and `ParseGeminiResponse()` SHALL successfully parse it into a GeminiResponse object without throwing JsonException.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation - Existing Functionality Unchanged

_For any_ operation that does NOT involve the JSON mode configuration, API request structure, or error handling improvements (specifically: caching, conversation history length management, health checks, ChatService intent routing, and EmployeeService calls), the fixed code SHALL produce exactly the same behavior as the original code, preserving all existing functionality.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 3.10**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `EmployeeManagement.Application/Services/AI/GeminiService.cs`

**Specific Changes**:

1. **Add JSON Schema Definition**:
   - Create private method `GetResponseSchema()` that returns the JSON schema object
   - Schema defines all ChatIntent enum values, confidence range, and entity structure
   - Schema marks intent, confidence, responseText, and entities as required

2. **Update CallGeminiApiAsync Method**:
   - Add `system_instruction` parameter at root level with system prompt
   - Change `responseMimeType` to `response_mime_type` in generationConfig
   - Add `response_schema` parameter with schema from GetResponseSchema()
   - Update conversation history to be included in user message context, not as separate content entries

3. **Refactor BuildFullPrompt Method**:
   - Rename to `BuildUserPromptWithHistory` for clarity
   - Remove system prompt from this method (now handled by system_instruction)
   - Format conversation history within user's context section
   - Return only the user-specific portion of the prompt

4. **Add Error Categorization**:
   - Create `GeminiErrorCategory` enum (InvalidApiKey, QuotaExceeded, InvalidRequest, ContentBlocked, ServiceUnavailable, Timeout, ParsingError, UnknownError)
   - Implement `HandleGeminiError` method with category-specific error messages
   - Implement `CategorizeHttpError` method to map status codes to categories

5. **Enhance ParseGeminiResponse Method**:
   - Add empty candidates check before processing
   - Add finish reason validation (detect SAFETY, RECITATION, OTHER blocks)
   - Split into `ParseGeminiResponse` and `ParseStructuredResponse` for better separation
   - Add null-safe helper methods: `TryGetInt`, `TryGetString`, `TryGetDecimal`
   - Add comprehensive logging at each parsing stage
   - Implement multi-layer fallback strategy

6. **Update ProcessChatMessageAsync Exception Handling**:
   - Replace generic catch with specific exception handlers
   - Catch HttpRequestException separately and categorize
   - Catch TimeoutException and map to Timeout category
   - Catch JsonException and map to ParsingError category
   - Log full exception details including stack traces

7. **Replace Conversation History Dictionary**:
   - Change `Dictionary<string, List<ConversationMessage>>` to `ConcurrentDictionary<string, List<ConversationMessage>>`
   - Update `UpdateConversationHistory` to use `GetOrAdd` and lock on list for mutations
   - Update `GetConversationMessages` helper to use TryGetValue with lock
   - Update `GetConversationHistoryAsync` to use TryGetValue with lock
   - Update `ClearConversationHistoryAsync` to use TryRemove

8. **Add Configuration Validation**:
   - Create `ValidateConfiguration()` private method
   - Call from constructor after field initialization
   - Validate API key format (starts with "AIza")
   - Validate model name format (starts with "gemini-")
   - Validate all numeric parameters within acceptable ranges
   - Throw InvalidOperationException with detailed error message listing all validation failures

**File**: `EmployeeManagement.Application/Services/AI/ChatContext.cs` (new file)

**Specific Changes**:

1. **Create ChatContext DTO**:
   - Add SessionId property
   - Add LastCreatedEmployeeId property
   - Add PendingDeleteEmployeeId and PendingDeleteEmployeeName properties
   - Add AdditionalData dictionary for extensibility
   - Implement `FromDictionary` factory method for backward compatibility
   - Implement `ToDictionary` conversion method

**File**: `EmployeeManagement.Application/Services/ChatService.cs`

**Specific Changes**:

1. **Update ProcessMessage Signature** (optional, for type safety):
   - Keep current signature for backward compatibility
   - Add internal conversion from Dictionary to ChatContext at method start
   - Use ChatContext throughout internal logic
   - Convert back to Dictionary for UpdatedContext in responses

2. **Update Context Usage** (minimal changes):
   - Replace `context["lastCreatedEmployee"]` with `context.LastCreatedEmployeeId`
   - Replace `context["pendingDeleteEmployee"]` with `context.PendingDeleteEmployeeId/Name`
   - All other context operations remain unchanged

**File**: `EmployeeManagement.Application/Services/AI/IGeminiService.cs`

**Specific Changes**:

1. **Update Interface Documentation**:
   - Add XML comments explaining that employeeContext can be null
   - Document that sessionId must be unique per conversation
   - Note that method is thread-safe for concurrent calls

2. **No Signature Changes**: Interface remains unchanged to preserve backward compatibility

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Create integration tests that call GeminiService with real API calls (using test API key) and assert that responses parse successfully. Run these tests on the UNFIXED code to observe parsing failures. Capture the raw API responses to confirm they are plain text instead of JSON.

**Test Cases**:
1. **Simple Greeting Test**: Send "hello" and observe that Gemini returns plain text like "Hello! How can I help?" which fails to parse (will fail on unfixed code)
2. **Intent Extraction Test**: Send "add employee named John" and observe that response is natural language, not JSON structure (will fail on unfixed code)
3. **API Response Structure Test**: Examine raw HTTP response to confirm absence of `response_mime_type` in request and plain text in response (will fail on unfixed code)
4. **Concurrent Session Test**: Send 5 concurrent requests to same session and observe potential dictionary corruption or lost messages (may fail on unfixed code)

**Expected Counterexamples**:
- JsonException thrown in ParseGeminiResponse with message "Unexpected character at start of JSON"
- Logged raw responses showing conversational text instead of JSON objects
- Fallback responses with confidence 0.3 and intent UNKNOWN
- Possible causes: missing JSON mode configuration, incorrect parameter names, no response schema

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds (any valid user message), the fixed function produces the expected behavior (successful JSON parsing and structured response).

**Pseudocode:**
```
FOR ALL userMessage IN [greetings, simple intents, complex intents, edge cases] DO
  response := GeminiService.ProcessChatMessageAsync_fixed(userMessage, sessionId, employeeContext)
  
  ASSERT response IS NOT NULL
  ASSERT response.Intent IN [valid ChatIntent enum values]
  ASSERT response.Confidence BETWEEN 0.0 AND 1.0
  ASSERT response.ResponseText IS NOT empty
  ASSERT response.ResponseText DOES NOT equal "I'm having trouble processing your request right now"
  
  IF intent is simple (GREETING, HELP) THEN
    ASSERT response.Confidence > 0.8
  END IF
  
  IF intent requires missing data (CREATE_EMPLOYEE with only name) THEN
    ASSERT response.Intent equals CLARIFICATION_NEEDED
    ASSERT response.Suggestions IS NOT empty
  END IF
END FOR
```

**Unit Tests**:
- Test JSON schema generation produces valid schema with all required fields
- Test snake_case parameter naming in request body
- Test system_instruction placement at root level
- Test error categorization for each HTTP status code
- Test thread-safe conversation history updates under concurrent access
- Test configuration validation catches invalid API keys, model names, and parameter ranges

**Integration Tests**:
- Test end-to-end message processing with real Gemini API
- Test greeting intent returns high confidence (> 0.8)
- Test help intent returns structured response
- Test CREATE_EMPLOYEE with missing fields returns CLARIFICATION_NEEDED
- Test invalid API key returns appropriate error message
- Test quota exceeded scenario (if testable)
- Test concurrent requests to same session maintain history integrity

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold (operations unrelated to JSON mode and error handling), the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL operation IN [caching, history trimming, health checks, ChatService routing] DO
  result_original := originalFunction(operation)
  result_fixed := fixedFunction(operation)
  
  ASSERT result_original EQUALS result_fixed
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain
- It catches edge cases that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for caching, history management, and health checks, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Caching Preservation**: Verify that identical messages within cache TTL return cached responses (test on unfixed, then verify on fixed)
2. **History Length Preservation**: Verify that conversation history is trimmed at MaxHistoryLength * 2 messages (test on unfixed, then verify on fixed)
3. **Health Check Preservation**: Verify that health check returns true for valid configuration, false for errors (test on unfixed, then verify on fixed)
4. **ChatService Routing Preservation**: Verify that each intent routes to the same handler method (test on unfixed, then verify on fixed)
5. **EmployeeService Integration Preservation**: Verify that CREATE, READ, UPDATE, DELETE operations call the same EmployeeService methods (test on unfixed, then verify on fixed)
6. **HTTP Timeout Preservation**: Verify that requests respect configured timeout (test on unfixed, then verify on fixed)

### Property-Based Tests

- Generate random user messages and verify all responses have valid structure (Intent in enum, Confidence in range, non-empty text)
- Generate random session IDs and verify conversation history isolation (messages don't leak between sessions)
- Generate random employee contexts and verify system prompt includes top 5 employees
- Generate concurrent requests to random sessions and verify no history corruption
- Generate random configurations within valid ranges and verify no validation errors
- Generate random configurations with invalid values and verify validation catches them

### Integration Tests

- Test full chatbot flow: greeting → list employees → create employee → confirm creation → search for employee
- Test error recovery: send invalid message → receive fallback → send valid message → receive proper response
- Test conversation context: create employee → reference "the employee I just created" → system uses conversation history
- Test concurrent sessions: multiple users chat simultaneously without interference
- Test API error scenarios: invalid API key, quota exceeded (if testable with test account), timeout
- Test safety filter edge case: send potentially blocked content → verify proper error handling
