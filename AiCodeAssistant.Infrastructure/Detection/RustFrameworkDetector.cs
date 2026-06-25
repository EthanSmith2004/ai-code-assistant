using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Infrastructure.Detection;

/// <summary>
/// Detects Rust projects (Cargo.toml) and a common web framework if present.
/// </summary>
public sealed class RustFrameworkDetector : FrameworkDetectorBase
{
    public override IReadOnlyList<FrameworkDetectionResult> Detect(ProjectScanResult scanResult)
    {
        return DetectFromManifest(
            scanResult,
            new[] { "Cargo.toml" },
            language: "Rust",
            baseFramework: "Rust",
            baseConfidence: 0.8,
            new[]
            {
                ("actix-web", "Actix Web"),
                ("axum", "Axum"),
                ("rocket", "Rocket"),
                ("warp", "Warp"),
                ("tonic", "Tonic"),
            });
    }
}
