namespace AiCodeAssistant.Domain.Contracts.Ai;

public class ExplainNodeRequest
{
    public NodeContext SelectedNode { get; set; } = new();

    public List<NodeRelationshipContext> IncomingRelationships { get; set; } = new();

    public List<NodeRelationshipContext> OutgoingRelationships { get; set; } = new();
}
