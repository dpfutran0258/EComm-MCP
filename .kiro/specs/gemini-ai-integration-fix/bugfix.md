# Bugfix Requirements Document

## Introduction

The Gemini AI chatbot integration is failing to process user messages correctly, returning generic fallback responses instead of understanding user intents. When users send natural language requests like "add yusuf", the system returns:

```json
{
  "responseText": "I'm having trouble processing your request right now. Please try again.",
  "type": "CLARIFICATION",
  "data": { "confidence": 0, "suggestions": [] }
}
```

This defeats the purpose of the AI-powered chatbot, as it cannot interpret user intents or extract entities from messages. The root cause is that `GeminiService` expects structured JSON responses but Gemini returns plain text because the `responseMimeType` parameter was removed and proper JSON schema mode is not being used.

**Impact**: Users cannot interact with the chatbot effectively, resulting in a broken AI feature that fails its primary purpose of natural language understanding.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a user sends any message to the chatbot THEN `GeminiService.ParseGeminiResponse()` fails to parse the plain text response from Gemini API

1.2 WHEN `ParseGeminiResponse()` throws an exception THEN the catch block returns a generic fallback response with confidence 0.3 and intent UNKNOWN

1.3 WHEN Gemini API returns plain text instead of JSON THEN the `JsonDocument.Parse(generatedText)` call fails with JsonException

1.4 WHEN the JSON parsing fails THEN the system logs "Error parsing Gemini response" but still returns confidence 0.3 instead of properly indicating complete failure

1.5 WHEN `responseMimeType` is omitted from the API request THEN Gemini returns natural language text instead of structured JSON

1.6 WHEN ChatController receives `Dictionary<string, object>` context THEN ChatService attempts to use it as employee context but the types don't match

1.7 WHEN function calling is not configured THEN Gemini attempts to return structured data in plain JSON format without validation

1.8 WHEN errors occur in `CallGeminiApiAsync()` THEN they are logged but the outer catch block masks them with generic messages

1.9 WHEN API responses contain blocked content or empty candidates THEN these edge cases are not handled and cause parsing failures

1.10 WHEN conversation history uses in-memory Dictionary THEN concurrent requests for the same session can cause race conditions

### Expected Behavior (Correct)

2.1 WHEN a user sends any message to the chatbot THEN the system SHALL use Gemini's JSON schema mode to guarantee structured JSON responses

2.2 WHEN Gemini API is called THEN the request SHALL include `response_mime_type: "application/json"` and a valid JSON schema in generation_config

2.3 WHEN Gemini returns a response THEN `ParseGeminiResponse()` SHALL successfully parse the guaranteed JSON structure

2.4 WHEN simple intents are detected (greeting, help) THEN Gemini SHALL return structured responses with confidence > 0.8

2.5 WHEN complex intents require missing information (e.g., "add yusuf" without email/department/salary) THEN Gemini SHALL return CLARIFICATION response with specific missing fields listed

2.6 WHEN ChatController receives context THEN it SHALL map `Dictionary<string, object>` to a structured ChatContext DTO before passing to ChatService

2.7 WHEN Gemini suggests database actions THEN the system SHALL validate all required parameters before execution and request clarification for missing data

2.8 WHEN Gemini API returns errors (quota exceeded, invalid key, blocked content) THEN the system SHALL log the specific error with full context and return meaningful error messages

2.9 WHEN Gemini API returns empty candidates or blocked responses THEN the system SHALL detect these cases and return appropriate error messages

2.10 WHEN multiple requests arrive for the same session THEN conversation history SHALL be thread-safe using ConcurrentDictionary

2.11 WHEN API calls are made THEN the system SHALL log request/response bodies when EnableDetailedLogging is true (without exposing API keys)

2.12 WHEN configuration is loaded THEN the system SHALL validate API key format, model name, and numeric parameters with clear error messages

### Unchanged Behavior (Regression Prevention)

3.1 WHEN the Gemini API is successfully called THEN the system SHALL CONTINUE TO use the existing HttpClient timeout configuration

3.2 WHEN caching is enabled THEN the system SHALL CONTINUE TO cache responses with the configured TTL

3.3 WHEN conversation history is retrieved THEN the system SHALL CONTINUE TO respect the MaxHistoryLength configuration

3.4 WHEN health checks are performed THEN the system SHALL CONTINUE TO return boolean indicating service availability

3.5 WHEN responses are logged THEN the system SHALL CONTINUE TO avoid logging sensitive employee PII beyond what's necessary for debugging

3.6 WHEN ChatService routes intents THEN the system SHALL CONTINUE TO use the existing switch-based routing pattern

3.7 WHEN employee operations are performed THEN the system SHALL CONTINUE TO use the existing EmployeeService methods without modification

3.8 WHEN HTTP errors occur THEN the system SHALL CONTINUE TO throw InvalidOperationException with appropriate error messages

3.9 WHEN system prompts are built THEN the system SHALL CONTINUE TO include employee context summaries (top 5 employees)

3.10 WHEN chat responses are returned THEN the system SHALL CONTINUE TO use the existing ChatResponse DTO structure
