using AiCodeAssistant.Domain.Contracts.Projects;

namespace AiCodeAssistant.Application.Interfaces;

public interface IProjectPersistenceService
{
    Task<ProjectDto> SaveProjectAsync(
        Guid userId,
        SaveProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectAnalysisDto> SaveAnalysisAsync(
        Guid userId,
        Guid projectId,
        SaveAnalysisRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectAnalysisDto>> GetProjectAnalysesAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<DashboardSummaryDto> GetDashboardSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
