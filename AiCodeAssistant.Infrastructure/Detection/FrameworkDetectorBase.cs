using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Infrastructure.Detection;

/// <summary>
/// Base class for framework detectors that recognise an ecosystem by its
/// manifest file (package.json, go.mod, pom.xml, ...) and refine the result by
/// reading that manifest's contents. File reads are best-effort and never throw.
/// </summary>
public abstract class FrameworkDetectorBase : IFrameworkDetector
{
    public abstract IReadOnlyList<FrameworkDetectionResult> Detect(ProjectScanResult scanResult);

    protected static string? FindFile(ProjectScanResult scanResult, string fileName)
    {
        return scanResult.FilePaths.FirstOrDefault(path =>
            Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    protected static bool HasFile(ProjectScanResult scanResult, string fileName)
    {
        return FindFile(scanResult, fileName) is not null;
    }

    /// <summary>
    /// Recognises an ecosystem from the first present manifest file, then adds a
    /// result for each known framework token found in that manifest's contents.
    /// </summary>
    protected static IReadOnlyList<FrameworkDetectionResult> DetectFromManifest(
        ProjectScanResult scanResult,
        IReadOnlyList<string> manifestFileNames,
        string language,
        string baseFramework,
        double baseConfidence,
        IReadOnlyList<(string Token, string Framework)> frameworks)
    {
        var manifestPath = manifestFileNames
            .Select(name => FindFile(scanResult, name))
            .FirstOrDefault(path => path is not null);

        if (manifestPath is null)
        {
            return Array.Empty<FrameworkDetectionResult>();
        }

        var results = new List<FrameworkDetectionResult>
        {
            new()
            {
                Language = language,
                Framework = baseFramework,
                Confidence = baseConfidence,
                Evidence = new List<string> { manifestPath }
            }
        };

        var content = ReadManifest(scanResult, manifestPath);
        if (!string.IsNullOrWhiteSpace(content))
        {
            foreach (var (token, framework) in frameworks)
            {
                if (content.Contains(token, StringComparison.OrdinalIgnoreCase))
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
        }

        return results;
    }

    protected static string ReadManifest(ProjectScanResult scanResult, string relativePath)
    {
        try
        {
            var rootFullPath = Path.GetFullPath(scanResult.RootPath);
            var fullPath = Path.GetFullPath(Path.Combine(
                rootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = rootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                             Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return File.ReadAllText(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
