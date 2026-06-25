namespace AiCodeAssistant.Infrastructure.Ai;

/// <summary>
/// Configuration for the Groq chat completion client. Bound from the "Groq"
/// configuration section. The API key must come from a secret source
/// (environment variable Groq__ApiKey or user secrets), never source control.
/// </summary>
public sealed class GroqOptions
{
    public const string SectionName = "Groq";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Groq model id. Groq rotates available models periodically; if this one is
    /// retired, set a current one via configuration without a code change.
    /// </summary>
    public string Model { get; set; } = "llama-3.3-70b-versatile";

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    public int TimeoutSeconds { get; set; } = 30;
}
