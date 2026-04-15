namespace AiCodeAssistant.Domain.Graph;

public class CodeEdge
{
    public string Id { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string Relationship { get; set; } = string.Empty;
}
