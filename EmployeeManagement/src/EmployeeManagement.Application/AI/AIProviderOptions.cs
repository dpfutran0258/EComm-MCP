namespace EmployeeManagement.Application.AI;

/// <summary>
/// Top-level AI configuration bound from the "AI" section in appsettings.
/// </summary>
public sealed class AIOptions
{
    public const string SectionName = "AI";

    /// <summary>
    /// Name of the default provider. Fixed to "OpenRouter" now.
    /// </summary>
    public string DefaultProvider { get; set; } = "OpenRouter";

    /// <summary>OpenRouter provider-specific settings.</summary>
    public OpenRouterProviderOptions OpenRouter { get; set; } = new();
}

/// <summary>Shared settings applicable to all providers.</summary>
public abstract class BaseProviderOptions
{
    /// <summary>API key for authenticating with the provider.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model identifier to use (provider-specific string).</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Maximum tokens the model should return per response.</summary>
    public int MaxOutputTokens { get; set; } = 2048;

    /// <summary>Sampling temperature — higher values produce more creative outputs.</summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>Nucleus sampling parameter.</summary>
    public double TopP { get; set; } = 0.95;

    /// <summary>Per-request HTTP timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 90;

    /// <summary>Emit detailed provider-level log entries when true.</summary>
    public bool EnableDetailedLogging { get; set; } = false;
}

/// <summary>OpenRouter–specific settings (covers NVIDIA Nemotron and other models).</summary>
public sealed class OpenRouterProviderOptions : BaseProviderOptions
{
    public OpenRouterProviderOptions()
    {
        Model = "nvidia/nemotron-3-ultra-550b-a55b:free";
        TimeoutSeconds = 120;
    }

    /// <summary>OpenRouter base URL.</summary>
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
}
