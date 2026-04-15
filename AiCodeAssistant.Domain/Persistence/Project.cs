namespace AiCodeAssistant.Domain.Persistence;

public class Project
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FrameworkType { get; set; } = string.Empty;

    public string SourceIdentifier { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<Analysis> Analyses { get; set; } = new();
}
