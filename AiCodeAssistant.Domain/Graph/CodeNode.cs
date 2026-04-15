namespace AiCodeAssistant.Domain.Graph;

public class CodeNode
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Layer { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string Framework { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
