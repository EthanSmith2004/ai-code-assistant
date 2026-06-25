namespace AiCodeAssistant.Domain.Contracts.Ai;

/// <summary>
/// A provider-agnostic chat completion prompt: a system instruction plus a user
/// message, with generation parameters. Kept deliberately small so any LLM
/// provider implementation can map it onto its own request shape.
/// </summary>
public sealed class ChatPrompt
{
    public string SystemPrompt { get; init; } = string.Empty;

    public string UserPrompt { get; init; } = string.Empty;

    public double Temperature { get; init; } = 0.4;

    public int MaxTokens { get; init; } = 700;
}
