using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Application.Interfaces;

public interface IProjectScanner
{
    Task<ProjectScanResult> ScanAsync(ProjectScanRequest request, CancellationToken cancellationToken = default);
}
