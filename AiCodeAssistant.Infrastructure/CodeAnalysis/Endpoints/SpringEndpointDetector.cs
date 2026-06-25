using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Endpoints;

/// <summary>
/// Detects Spring MVC / WebFlux endpoints in Java and Kotlin from
/// <c>@GetMapping</c>, <c>@PostMapping</c>, ... and <c>@RequestMapping</c>
/// annotations. Class-level prefixes are not combined (the method-level path is
/// reported), which keeps the heuristic simple and dependency-free.
/// </summary>
public sealed partial class SpringEndpointDetector : EndpointDetectorBase
{
    protected override IReadOnlyCollection<string> Extensions { get; } = new[] { ".java", ".kt" };

    public override IEnumerable<DetectedEndpoint> Detect(string sourceText)
    {
        foreach (Match match in MappingRegex().Matches(sourceText))
        {
            yield return new DetectedEndpoint(NormalizeMethod(match.Groups["verb"].Value), match.Groups["path"].Value);
        }

        foreach (Match match in RequestMappingRegex().Matches(sourceText))
        {
            var methodMatch = RequestMethodRegex().Match(match.Groups["args"].Value);
            var method = methodMatch.Success ? NormalizeMethod(methodMatch.Groups["m"].Value) : "ANY";
            yield return new DetectedEndpoint(method, match.Groups["path"].Value);
        }
    }

    // @GetMapping("/x"), @PostMapping(value = "/x"), @PutMapping(path = "/x")
    [GeneratedRegex(@"@(?<verb>Get|Post|Put|Delete|Patch)Mapping\s*\(\s*(?:(?:value|path)\s*=\s*)?\{?\s*['""](?<path>[^'""]+)['""]",
        RegexOptions.CultureInvariant)]
    private static partial Regex MappingRegex();

    // @RequestMapping(value = "/x", method = RequestMethod.GET)
    [GeneratedRegex(@"@RequestMapping\s*\(\s*(?:(?:value|path)\s*=\s*)?\{?\s*['""](?<path>[^'""]+)['""](?<args>[^)]*)",
        RegexOptions.CultureInvariant)]
    private static partial Regex RequestMappingRegex();

    [GeneratedRegex(@"RequestMethod\.(?<m>GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)", RegexOptions.CultureInvariant)]
    private static partial Regex RequestMethodRegex();
}
