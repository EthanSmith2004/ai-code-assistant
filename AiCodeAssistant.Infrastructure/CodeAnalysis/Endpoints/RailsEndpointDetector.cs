using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Endpoints;

/// <summary>
/// Detects Ruby on Rails routes declared in the routing DSL
/// (<c>get '/users'</c>, <c>post '/login'</c>). The DSL form (a verb followed by
/// a string, with no receiver/dot) distinguishes routes from ordinary method
/// calls such as <c>obj.get(...)</c>.
/// </summary>
public sealed partial class RailsEndpointDetector : EndpointDetectorBase
{
    protected override IReadOnlyCollection<string> Extensions { get; } = new[] { ".rb" };

    public override IEnumerable<DetectedEndpoint> Detect(string sourceText)
    {
        foreach (Match match in RouteRegex().Matches(sourceText))
        {
            yield return new DetectedEndpoint(NormalizeMethod(match.Groups["verb"].Value), match.Groups["path"].Value);
        }
    }

    // get '/users', post "/login", patch '/items/:id'  (no dot before the verb)
    [GeneratedRegex(@"(?<![\w.])(?<verb>get|post|put|patch|delete)\s+['""](?<path>/[^'""]+)['""]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RouteRegex();
}
