using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Infrastructure.Detection;

/// <summary>
/// Detects Go projects (go.mod) and a common HTTP framework if one is required.
/// </summary>
public sealed class GoFrameworkDetector : FrameworkDetectorBase
{
    public override IReadOnlyList<FrameworkDetectionResult> Detect(ProjectScanResult scanResult)
    {
        return DetectFromManifest(
            scanResult,
            new[] { "go.mod" },
            language: "Go",
            baseFramework: "Go",
            baseConfidence: 0.8,
            new[]
            {
                ("gin-gonic/gin", "Gin"),
                ("labstack/echo", "Echo"),
                ("gofiber/fiber", "Fiber"),
                ("go-chi/chi", "Chi"),
                ("gorilla/mux", "Gorilla Mux"),
            });
    }
}
