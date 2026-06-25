using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Endpoints;

/// <summary>Shared extension matching for endpoint detectors.</summary>
public abstract class EndpointDetectorBase : IEndpointDetector
{
    protected abstract IReadOnlyCollection<string> Extensions { get; }

    public bool CanHandle(string relativePath)
    {
        return Extensions.Any(extension => relativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    public abstract IEnumerable<DetectedEndpoint> Detect(string sourceText);

    protected static string NormalizeMethod(string verb)
    {
        var upper = verb.Trim().ToUpperInvariant();
        return upper switch
        {
            "ALL" or "USE" or "MAP" or "MATCH" or "ANY" or "REQUEST" => "ANY",
            _ => upper
        };
    }
}
