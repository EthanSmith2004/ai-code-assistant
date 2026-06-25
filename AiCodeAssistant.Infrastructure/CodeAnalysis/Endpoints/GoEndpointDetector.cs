using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Endpoints;

/// <summary>
/// Detects Go HTTP endpoints from router registrations used by Gin, Echo, Chi,
/// and Gorilla (<c>r.GET("/x", h)</c>, <c>e.POST("/x", h)</c>, <c>r.Get("/x", h)</c>)
/// as well as the standard library (<c>http.HandleFunc("/x", h)</c>).
/// </summary>
public sealed partial class GoEndpointDetector : EndpointDetectorBase
{
    protected override IReadOnlyCollection<string> Extensions { get; } = new[] { ".go" };

    public override IEnumerable<DetectedEndpoint> Detect(string sourceText)
    {
        foreach (Match match in RouterRegex().Matches(sourceText))
        {
            yield return new DetectedEndpoint(NormalizeMethod(match.Groups["verb"].Value), match.Groups["path"].Value);
        }

        foreach (Match match in HandleFuncRegex().Matches(sourceText))
        {
            yield return new DetectedEndpoint("ANY", match.Groups["path"].Value);
        }
    }

    // r.GET("/x", h), e.POST("/x", h), r.Get("/x", h) — verbs are upper (Gin/Echo) or Pascal (Chi)
    [GeneratedRegex(@"\b\w+\.(?<verb>GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS|Get|Post|Put|Delete|Patch|Head|Options)\s*\(\s*""(?<path>/[^""]*)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex RouterRegex();

    // http.HandleFunc("/x", handler), mux.HandleFunc("/x", handler)
    [GeneratedRegex(@"\.HandleFunc\s*\(\s*""(?<path>/[^""]*)""", RegexOptions.CultureInvariant)]
    private static partial Regex HandleFuncRegex();
}
