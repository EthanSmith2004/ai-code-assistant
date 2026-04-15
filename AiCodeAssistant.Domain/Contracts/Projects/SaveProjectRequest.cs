namespace AiCodeAssistant.Domain.Contracts.Projects;

public class SaveProjectRequest
{
    public string Name { get; set; } = string.Empty;

    public string FrameworkType { get; set; } = string.Empty;

    public string SourceIdentifier { get; set; } = string.Empty;
}
