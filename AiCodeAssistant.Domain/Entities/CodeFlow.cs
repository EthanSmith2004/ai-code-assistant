namespace AiCodeAssistant.Domain.Entities;

public class CodeFlow
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> NodeIds { get; set; } = new();
}