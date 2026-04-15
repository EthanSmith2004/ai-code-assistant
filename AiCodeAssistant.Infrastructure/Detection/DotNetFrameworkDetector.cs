using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Infrastructure.Detection;

public class DotNetFrameworkDetector : IFrameworkDetector
{
    public IReadOnlyList<FrameworkDetectionResult> Detect(ProjectScanResult scanResult)
    {
        var results = new List<FrameworkDetectionResult>();
        var files = scanResult.FilePaths;

        var dotNetEvidence = files
            .Where(path =>
                path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        if (dotNetEvidence.Any())
        {
            results.Add(new FrameworkDetectionResult
            {
                Language = "CSharp",
                Framework = ".NET",
                Confidence = 0.9,
                Evidence = dotNetEvidence
            });
        }

        var blazorEvidence = files
            .Where(path =>
                path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("wwwroot", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        if (blazorEvidence.Any())
        {
            results.Add(new FrameworkDetectionResult
            {
                Language = "CSharp",
                Framework = "Blazor",
                Confidence = 0.8,
                Evidence = blazorEvidence
            });
        }

        var aspNetEvidence = files
            .Where(path =>
                path.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase) ||
                path.Contains($"{Path.DirectorySeparatorChar}Controllers{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Controllers/", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        if (aspNetEvidence.Any())
        {
            results.Add(new FrameworkDetectionResult
            {
                Language = "CSharp",
                Framework = "ASP.NET Core",
                Confidence = 0.75,
                Evidence = aspNetEvidence
            });
        }

        return results;
    }
}
