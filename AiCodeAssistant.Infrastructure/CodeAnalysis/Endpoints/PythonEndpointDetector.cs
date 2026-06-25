using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Endpoints;

/// <summary>
/// Detects Python endpoints: FastAPI/router decorators
/// (<c>@app.get("/x")</c>, <c>@router.post("/x")</c>), Flask routes
/// (<c>@app.route("/x", methods=["POST"])</c>), and Django URL patterns
/// (<c>path("x/", view)</c>).
/// </summary>
public sealed partial class PythonEndpointDetector : EndpointDetectorBase
{
    protected override IReadOnlyCollection<string> Extensions { get; } = new[] { ".py" };

    public override IEnumerable<DetectedEndpoint> Detect(string sourceText)
    {
        foreach (Match match in FastApiRegex().Matches(sourceText))
        {
            yield return new DetectedEndpoint(NormalizeMethod(match.Groups["verb"].Value), match.Groups["path"].Value);
        }

        foreach (Match match in FlaskRouteRegex().Matches(sourceText))
        {
            var path = match.Groups["path"].Value;
            var methodsMatch = FlaskMethodsRegex().Match(match.Groups["rest"].Value);

            if (!methodsMatch.Success)
            {
                yield return new DetectedEndpoint("GET", path);
                continue;
            }

            foreach (Match method in MethodTokenRegex().Matches(methodsMatch.Groups["methods"].Value))
            {
                yield return new DetectedEndpoint(NormalizeMethod(method.Groups["m"].Value), path);
            }
        }

        foreach (Match match in DjangoPathRegex().Matches(sourceText))
        {
            yield return new DetectedEndpoint("ANY", match.Groups["path"].Value);
        }
    }

    [GeneratedRegex(@"@[\w.]+\.(?<verb>get|post|put|delete|patch|options|head)\s*\(\s*['""](?<path>[^'""]+)['""]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FastApiRegex();

    [GeneratedRegex(@"@[\w.]+\.route\s*\(\s*['""](?<path>[^'""]+)['""](?<rest>[^)]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FlaskRouteRegex();

    // The methods=[...] list captured from a Flask route's trailing arguments.
    [GeneratedRegex(@"methods\s*=\s*\[(?<methods>[^\]]*)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FlaskMethodsRegex();

    [GeneratedRegex(@"['""](?<m>GET|POST|PUT|DELETE|PATCH|OPTIONS|HEAD)['""]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MethodTokenRegex();

    // Django: path("route/", view, ...) or re_path(r"^route$", view, ...)
    [GeneratedRegex(@"\b(?:re_path|path|url)\s*\(\s*r?['""](?<path>[^'""]+)['""]\s*,",
        RegexOptions.CultureInvariant)]
    private static partial Regex DjangoPathRegex();
}
