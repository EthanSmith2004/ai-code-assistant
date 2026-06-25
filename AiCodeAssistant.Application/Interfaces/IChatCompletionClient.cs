using AiCodeAssistant.Domain.Contracts.Ai;

namespace AiCodeAssistant.Application.Interfaces;

/// <summary>
/// Abstraction over a text generation LLM provider. Implementations live in the
/// Infrastructure layer so the Application layer stays free of HTTP/provider
/// concerns and can be reused beyond explanations (e.g. analysis enrichment).
/// </summary>
public interface IChatCompletionClient
{
    /// <summary>
    /// True when the client has everything it needs to make a real call
    /// (e.g. an API key). Callers should fall back to a non-AI path when false.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Generates a completion for the given prompt. Returns <c>null</c> when the
    /// client is not configured or the provider call fails, so callers can fall
    /// back gracefully instead of surfacing an error.
    /// </summary>
    Task<string?> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default);
}
