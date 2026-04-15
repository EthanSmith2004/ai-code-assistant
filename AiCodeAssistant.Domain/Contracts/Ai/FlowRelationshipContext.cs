namespace AiCodeAssistant.Domain.Contracts.Ai;

public class FlowRelationshipContext
{
    public string Relationship { get; set; } = string.Empty;

    public NodeContext SourceNode { get; set; } = new();

    public NodeContext TargetNode { get; set; } = new();
}
