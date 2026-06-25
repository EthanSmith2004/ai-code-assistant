using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Endpoints;

/// <summary>
/// Detects endpoints in JavaScript/TypeScript: Express/Koa/Fastify route calls
/// (<c>app.get('/path', ...)</c>, <c>router.post('/path', ...)</c>) and NestJS
/// route decorators (<c>@Get('path')</c>, <c>@Post()</c>). Only paths beginning
/// with "/" are treated as routes for the call form, to avoid matching unrelated
/// method calls.
/// </summary>
public sealed partial class NodeEndpointDetector : EndpointDetectorBase
{
    protected override IReadOnlyCollection<string> Extensions { get; } = new[]
    {
        ".js", ".jsx", ".ts", ".tsx", ".mjs", ".cjs", ".mts", ".cts"
    };

    public override IEnumerable<DetectedEndpoint> Detect(string sourceText)
    {
        foreach (Match match in RouteCallRegex().Matches(sourceText))
        {
            yield return new DetectedEndpoint(NormalizeMethod(match.Groups["verb"].Value), match.Groups["path"].Value);
        }

        foreach (Match match in NestDecoratorRegex().Matches(sourceText))
        {
            var path = match.Groups["path"].Success ? match.Groups["path"].Value : "/";
            yield return new DetectedEndpoint(NormalizeMethod(match.Groups["verb"].Value), path);
        }
    }

    // app.get('/path', ...), router.post(`/path`, ...), api.delete("/path", ...)
    [GeneratedRegex(@"\b[\w$]+\.(?<verb>get|post|put|delete|patch|options|head|all)\s*\(\s*['""`](?<path>/[^'""`]*)['""`]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RouteCallRegex();

    // @Get('path'), @Post(), @Patch("path") — NestJS controllers
    [GeneratedRegex(@"@(?<verb>Get|Post|Put|Delete|Patch|Options|Head|All)\s*\(\s*(?:['""`](?<path>[^'""`]*)['""`])?\s*\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex NestDecoratorRegex();
}
