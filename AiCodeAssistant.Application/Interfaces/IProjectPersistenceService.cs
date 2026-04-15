using AiCodeAssistant.Domain.Contracts.Projects;

namespace AiCodeAssistant.Application.Interfaces;

public interface IProjectPersistenceService
{
    Task<ProjectDto> SaveProjectAsync(SaveProjectRequest request, CancellationToken cancellationToken = default);

    Task<ProjectAnalysisDto> SaveAnalysisAsync(
        Guid projectId,
        SaveAnalysisRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectAnalysisDto>> GetProjectAnalysesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
}
