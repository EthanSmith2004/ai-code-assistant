using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Infrastructure.Detection;

/// <summary>
/// Detects JVM projects (Maven/Gradle) and frameworks such as Spring Boot,
/// Quarkus, or Micronaut from the build manifest.
/// </summary>
public sealed class JvmFrameworkDetector : FrameworkDetectorBase
{
    public override IReadOnlyList<FrameworkDetectionResult> Detect(ProjectScanResult scanResult)
    {
        return DetectFromManifest(
            scanResult,
            new[] { "pom.xml", "build.gradle", "build.gradle.kts" },
            language: "Java",
            baseFramework: "JVM",
            baseConfidence: 0.7,
            new[]
            {
                ("spring-boot", "Spring Boot"),
                ("springframework", "Spring"),
                ("quarkus", "Quarkus"),
                ("micronaut", "Micronaut"),
                ("io.dropwizard", "Dropwizard"),
            });
    }
}
