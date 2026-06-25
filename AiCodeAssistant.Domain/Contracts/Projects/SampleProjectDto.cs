namespace AiCodeAssistant.Domain.Contracts.Projects;

/// <summary>A bundled demo codebase that users can scan to try CodeSight.</summary>
public class SampleProjectDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Framework { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Absolute server path the scanner reads.</summary>
    public string Path { get; set; } = string.Empty;
}
