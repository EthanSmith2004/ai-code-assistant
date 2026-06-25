using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Contracts.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiCodeAssistant.Infrastructure.Ai;

/// <summary>
/// <see cref="IChatCompletionClient"/> backed by Groq's OpenAI-compatible
/// chat completions API. Never throws to callers: on any failure it logs and
/// returns <c>null</c> so the explanation path can fall back to a template.
/// </summary>
public sealed class GroqChatClient : IChatCompletionClient
{
    private readonly HttpClient _httpClient;
    private readonly GroqOptions _options;
    private readonly ILogger<GroqChatClient> _logger;

    public GroqChatClient(HttpClient httpClient, IOptions<GroqOptions> options, ILogger<GroqChatClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds <= 0 ? 30 : _options.TimeoutSeconds);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string?> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var payload = new
        {
            model = _options.Model,
            temperature = prompt.Temperature,
            max_tokens = prompt.MaxTokens,
            messages = new object[]
            {
                new { role = "system", content = prompt.SystemPrompt },
                new { role = "user", content = prompt.UserPrompt }
            }
        };

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Groq completion failed ({StatusCode}). Model '{Model}'. Response: {Body}",
                    (int)response.StatusCode, _options.Model, Truncate(body));
                return null;
            }

            return ExtractContent(body);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "Groq completion request error against {Endpoint}.", endpoint);
            return null;
        }
    }

    private static string? ExtractContent(string body)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
            {
                var text = content.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
        }

        return null;
    }

    private static string Truncate(string value)
    {
        return value.Length <= 500 ? value : value[..500];
    }
}
