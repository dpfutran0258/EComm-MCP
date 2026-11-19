using EmployeeManagement.Application.DTOs;

namespace EmployeeManagement.Application.AI;

/// <summary>
/// Provider-independent AI response contract.
/// Every provider adapter maps its raw response into this structure.
/// </summary>
public sealed record AIResponse
{
    /// <summary>Natural language response shown to the user.</summary>
    public string ResponseText { get; init; } = string.Empty;

    /// <summary>Detected intent of the user's message.</summary>
    public ChatIntent Intent { get; init; } = ChatIntent.UNKNOWN;

    /// <summary>Confidence score between 0.0 and 1.0.</summary>
    public decimal Confidence { get; init; }

    /// <summary>Entities extracted from the user's message.</summary>
    public ExtractedEntities Entities { get; init; } = new();

    /// <summary>Suggested follow-up actions or clarifications.</summary>
    public List<string> Suggestions { get; init; } = new();

    /// <summary>Telemetry and provider-specific data.</summary>
    public AIResponseMetadata Metadata { get; init; } = new();

    /// <summary>Returns a fallback response indicating a provider failure.</summary>
    public static AIResponse Fallback(string providerName, string errorDetail) => new()
    {
        ResponseText = "I'm having trouble processing your request right now. Please try again in a moment.",
        Intent = ChatIntent.UNKNOWN,
        Confidence = 0m,
        Metadata = new AIResponseMetadata
        {
            Provider = providerName,
            Error = errorDetail
        }
    };
}

/// <summary>Telemetry attached to every AI response.</summary>
public sealed record AIResponseMetadata
{
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public int RetryCount { get; init; }
    public string? Error { get; init; }
    public Dictionary<string, object> ProviderSpecific { get; init; } = new();
}

/// <summary>
/// Entities extracted by the AI from the user's message.
/// Supports both single-employee and bulk operations.
/// </summary>
public sealed class ExtractedEntities
{
    public int? EmployeeId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
    public decimal? Salary { get; set; }
    public string? SearchQuery { get; set; }

    /// <summary>Populated when the user provides multiple employee records (e.g. a pasted table).</summary>
    public List<BulkEmployeeData>? BulkEmployees { get; set; }

    /// <summary>Type of bulk operation the AI detected.</summary>
    public BulkOperationType? BulkOperation { get; set; }

    /// <summary>Arbitrary additional data extracted by provider-specific logic.</summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>A single employee record extracted from a bulk input (table, CSV, etc.).</summary>
public sealed class BulkEmployeeData
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
    public decimal? Salary { get; set; }
}

/// <summary>Bulk operation type detected from user intent.</summary>
public enum BulkOperationType
{
    CREATE,
    UPDATE,
    DELETE,
    SEARCH
}
