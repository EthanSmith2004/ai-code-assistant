using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Application.Interfaces;

public interface IFrameworkDetector
{
    IReadOnlyList<FrameworkDetectionResult> Detect(ProjectScanResult scanResult);
}
