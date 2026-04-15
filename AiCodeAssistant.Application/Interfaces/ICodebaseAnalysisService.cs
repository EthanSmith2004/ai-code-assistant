using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Graph;

namespace AiCodeAssistant.Application.Interfaces;

public interface ICodebaseAnalysisService
{
    Task<CodeGraph> AnalyzeAsync(ProjectScanRequest request, CancellationToken cancellationToken = default);
}
