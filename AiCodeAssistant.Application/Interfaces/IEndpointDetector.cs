namespace AiCodeAssistant.Application.Interfaces;

/// <summary>A web/API endpoint discovered in a source file.</summary>
public sealed record DetectedEndpoint(string HttpMethod, string Route);

/// <summary>
/// Detects HTTP endpoints for one framework family (ASP.NET, Express, FastAPI,
/// Spring, Gin, Rails, ...). Implementations are regex/heuristic based and live
/// in the Infrastructure layer. This is what lets the app surface endpoints and
/// endpoint flows for any stack, not just .NET.
/// </summary>
public interface IEndpointDetector
{
    /// <summary>True when this detector understands the given file extension.</summary>
    bool CanHandle(string relativePath);

    /// <summary>Returns the endpoints declared in the given source text.</summary>
    IEnumerable<DetectedEndpoint> Detect(string sourceText);
}
