using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Graph;

namespace AiCodeAssistant.Application.Interfaces;

public interface ICodebaseAnalyzer
{
    Task<CodeGraph> AnalyzeAsync(
        ProjectScanResult scanResult,
        IReadOnlyList<FrameworkDetectionResult> detections,
        CancellationToken cancellationToken = default);
}
