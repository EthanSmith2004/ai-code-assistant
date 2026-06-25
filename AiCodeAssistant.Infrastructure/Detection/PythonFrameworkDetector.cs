using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Infrastructure.Detection;

/// <summary>
/// Detects Python projects and their web framework (Django, Flask, FastAPI, ...)
/// from a dependency manifest.
/// </summary>
public sealed class PythonFrameworkDetector : FrameworkDetectorBase
{
    public override IReadOnlyList<FrameworkDetectionResult> Detect(ProjectScanResult scanResult)
    {
        return DetectFromManifest(
            scanResult,
            new[] { "requirements.txt", "pyproject.toml", "Pipfile", "setup.py" },
            language: "Python",
            baseFramework: "Python",
            baseConfidence: 0.7,
            new[]
            {
                ("django", "Django"),
                ("flask", "Flask"),
                ("fastapi", "FastAPI"),
                ("tornado", "Tornado"),
                ("aiohttp", "aiohttp"),
                ("sanic", "Sanic"),
                ("pyramid", "Pyramid"),
            });
    }
}
