namespace AiCodeAssistant.Domain.Persistence;

public class Analysis
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public int FileCount { get; set; }

    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public int EndpointCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public Project? Project { get; set; }
}
