using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Graph;

namespace AiCodeAssistant.Application.Services;

public class CodebaseAnalysisService : ICodebaseAnalysisService
{
    private readonly IProjectScanner _projectScanner;
    private readonly IEnumerable<IFrameworkDetector> _frameworkDetectors;
    private readonly ICodebaseAnalyzer _codebaseAnalyzer;

    public CodebaseAnalysisService(
        IProjectScanner projectScanner,
        IEnumerable<IFrameworkDetector> frameworkDetectors,
        ICodebaseAnalyzer codebaseAnalyzer)
    {
        _projectScanner = projectScanner;
        _frameworkDetectors = frameworkDetectors;
        _codebaseAnalyzer = codebaseAnalyzer;
    }

    public async Task<CodeGraph> AnalyzeAsync(ProjectScanRequest request, CancellationToken cancellationToken = default)
    {
        var scanResult = await _projectScanner.ScanAsync(request, cancellationToken);
        var detections = _frameworkDetectors
            .SelectMany(detector => detector.Detect(scanResult))
            .Where(detection => detection.Confidence > 0)
            .OrderByDescending(detection => detection.Confidence)
            .ToList();

        return await _codebaseAnalyzer.AnalyzeAsync(scanResult, detections, cancellationToken);
    }
}
