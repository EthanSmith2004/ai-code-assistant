using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Contracts.Projects;
using AiCodeAssistant.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiCodeAssistant.Infrastructure.Persistence;

public class ProjectPersistenceService : IProjectPersistenceService
{
    private readonly CodeSightDbContext _dbContext;

    public ProjectPersistenceService(CodeSightDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectDto> SaveProjectAsync(
        Guid userId,
        SaveProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var sourceIdentifier = NormalizeRequired(request.SourceIdentifier, "Source identifier");
        var name = NormalizeOptional(request.Name, GetFallbackProjectName(sourceIdentifier));
        var frameworkType = NormalizeOptional(request.FrameworkType, "Unknown");
        var now = DateTime.UtcNow;

        var project = await _dbContext.Projects
            .Include(existingProject => existingProject.Analyses)
            .FirstOrDefaultAsync(
                existingProject =>
                    existingProject.UserId == userId &&
                    existingProject.SourceIdentifier == sourceIdentifier,
                cancellationToken);

        if (project is null)
        {
            project = new Project
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SourceIdentifier = sourceIdentifier,
                CreatedAt = now
            };

            _dbContext.Projects.Add(project);
        }

        project.Name = name;
        project.FrameworkType = frameworkType;
        project.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToProjectDto(project);
    }

    public async Task<ProjectAnalysisDto> SaveAnalysisAsync(
        Guid userId,
        Guid projectId,
        SaveAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(
                existingProject =>
                    existingProject.Id == projectId &&
                    existingProject.UserId == userId,
                cancellationToken);

        if (project is null)
        {
            throw new KeyNotFoundException($"Project '{projectId}' was not found.");
        }

        var now = DateTime.UtcNow;
        var analysis = new Analysis
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Summary = NormalizeOptional(request.Summary, "Saved project analysis."),
            FileCount = Math.Max(request.FileCount, 0),
            NodeCount = Math.Max(request.NodeCount, 0),
            EdgeCount = Math.Max(request.EdgeCount, 0),
            EndpointCount = Math.Max(request.EndpointCount, 0),
            CreatedAt = now
        };

        project.UpdatedAt = now;
        _dbContext.Analyses.Add(analysis);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToAnalysisDto(analysis);
    }

    public async Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var projects = await _dbContext.Projects
            .AsNoTracking()
            .Include(project => project.Analyses)
            .Where(project => project.UserId == userId)
            .OrderByDescending(project => project.UpdatedAt)
            .ToListAsync(cancellationToken);

        return projects.Select(ToProjectDto).ToList();
    }

    public async Task<IReadOnlyList<ProjectAnalysisDto>> GetProjectAnalysesAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Analyses
            .AsNoTracking()
            .Where(analysis =>
                analysis.ProjectId == projectId &&
                analysis.Project != null &&
                analysis.Project.UserId == userId)
            .OrderByDescending(analysis => analysis.CreatedAt)
            .Select(analysis => new ProjectAnalysisDto
            {
                Id = analysis.Id,
                ProjectId = analysis.ProjectId,
                Summary = analysis.Summary,
                FileCount = analysis.FileCount,
                NodeCount = analysis.NodeCount,
                EdgeCount = analysis.EdgeCount,
                EndpointCount = analysis.EndpointCount,
                CreatedAt = analysis.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var projects = await _dbContext.Projects
            .AsNoTracking()
            .Include(project => project.Analyses)
            .Where(project => project.UserId == userId)
            .OrderByDescending(project => project.UpdatedAt)
            .ToListAsync(cancellationToken);
        var analyses = projects
            .SelectMany(project => project.Analyses.Select(analysis => new { Project = project, Analysis = analysis }))
            .ToList();
        var latestAnalysis = analyses
            .OrderByDescending(item => item.Analysis.CreatedAt)
            .FirstOrDefault();

        return new DashboardSummaryDto
        {
            TotalProjects = projects.Count,
            TotalAnalyses = analyses.Count,
            TotalFilesAnalyzed = analyses.Sum(item => item.Analysis.FileCount),
            TotalNodesAnalyzed = analyses.Sum(item => item.Analysis.NodeCount),
            TotalEdgesAnalyzed = analyses.Sum(item => item.Analysis.EdgeCount),
            TotalEndpointsDiscovered = analyses.Sum(item => item.Analysis.EndpointCount),
            AverageNodeCount = analyses.Count == 0 ? 0 : analyses.Average(item => item.Analysis.NodeCount),
            LatestProjectName = latestAnalysis?.Project.Name ?? string.Empty,
            LatestAnalysisAt = latestAnalysis?.Analysis.CreatedAt
        };
    }

    private static ProjectDto ToProjectDto(Project project)
    {
        var latestAnalysis = project.Analyses
            .OrderByDescending(analysis => analysis.CreatedAt)
            .FirstOrDefault();

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            FrameworkType = project.FrameworkType,
            SourceIdentifier = project.SourceIdentifier,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            AnalysisCount = project.Analyses.Count,
            LatestAnalysis = latestAnalysis is null ? null : ToAnalysisDto(latestAnalysis)
        };
    }

    private static ProjectAnalysisDto ToAnalysisDto(Analysis analysis)
    {
        return new ProjectAnalysisDto
        {
            Id = analysis.Id,
            ProjectId = analysis.ProjectId,
            Summary = analysis.Summary,
            FileCount = analysis.FileCount,
            NodeCount = analysis.NodeCount,
            EdgeCount = analysis.EdgeCount,
            EndpointCount = analysis.EndpointCount,
            CreatedAt = analysis.CreatedAt
        };
    }

    private static string NormalizeRequired(string value, string displayName)
    {
        var normalizedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            throw new ArgumentException($"{displayName} is required.");
        }

        return normalizedValue;
    }

    private static string NormalizeOptional(string value, string fallback)
    {
        var normalizedValue = value.Trim();
        return string.IsNullOrWhiteSpace(normalizedValue) ? fallback : normalizedValue;
    }

    private static string GetFallbackProjectName(string sourceIdentifier)
    {
        var trimmed = sourceIdentifier.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            '/',
            '\\');

        return Path.GetFileName(trimmed) is { Length: > 0 } name
            ? name
            : sourceIdentifier;
    }
}
