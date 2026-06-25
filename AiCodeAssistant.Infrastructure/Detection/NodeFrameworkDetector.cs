using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Infrastructure.Detection;

/// <summary>
/// Detects the Node.js ecosystem from package.json and refines it to a specific
/// framework (React, Next.js, Vue, Angular, Svelte, Express, NestJS, ...) by
/// inspecting declared dependencies. Also flags TypeScript when configured.
/// </summary>
public sealed class NodeFrameworkDetector : FrameworkDetectorBase
{
    private static readonly (string Dependency, string Framework)[] KnownFrameworks =
    {
        ("next", "Next.js"),
        ("@angular/core", "Angular"),
        ("@nestjs/core", "NestJS"),
        ("nuxt", "Nuxt"),
        ("svelte", "Svelte"),
        ("vue", "Vue"),
        ("react", "React"),
        ("@remix-run/react", "Remix"),
        ("express", "Express"),
        ("koa", "Koa"),
        ("fastify", "Fastify"),
    };

    public override IReadOnlyList<FrameworkDetectionResult> Detect(ProjectScanResult scanResult)
    {
        var manifestPath = FindFile(scanResult, "package.json");
        if (manifestPath is null)
        {
            return Array.Empty<FrameworkDetectionResult>();
        }

        var language = HasFile(scanResult, "tsconfig.json") ? "TypeScript" : "JavaScript";
        var results = new List<FrameworkDetectionResult>
        {
            new()
            {
                Language = language,
                Framework = "Node.js",
                Confidence = 0.7,
                Evidence = new List<string> { manifestPath }
            }
        };

        var manifest = ReadManifest(scanResult, manifestPath);
        if (string.IsNullOrWhiteSpace(manifest))
        {
            return results;
        }

        foreach (var (dependency, framework) in KnownFrameworks)
        {
            if (manifest.Contains($"\"{dependency}\"", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new FrameworkDetectionResult
                {
                    Language = language,
                    Framework = framework,
                    Confidence = 0.85,
                    Evidence = new List<string> { manifestPath }
                });
            }
        }

        return results;
    }
}
