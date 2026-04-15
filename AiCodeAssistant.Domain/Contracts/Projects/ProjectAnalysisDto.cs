namespace AiCodeAssistant.Domain.Contracts.Projects;

public class ProjectAnalysisDto
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public int FileCount { get; set; }

    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public int EndpointCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
