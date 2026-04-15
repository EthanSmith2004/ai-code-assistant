namespace AiCodeAssistant.Domain.Contracts.Ai;

public class NodeRelationshipContext
{
    public string Relationship { get; set; } = string.Empty;

    public NodeContext Node { get; set; } = new();
}
