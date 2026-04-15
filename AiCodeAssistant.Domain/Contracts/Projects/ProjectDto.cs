namespace AiCodeAssistant.Domain.Contracts.Projects;

public class ProjectDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FrameworkType { get; set; } = string.Empty;

    public string SourceIdentifier { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int AnalysisCount { get; set; }

    public ProjectAnalysisDto? LatestAnalysis { get; set; }
}
