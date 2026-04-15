namespace AiCodeAssistant.Domain.Contracts.Ai;

public class ExplainFlowRequest
{
    public string FlowId { get; set; } = string.Empty;

    public string FlowName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<FlowNodeContext> OrderedNodes { get; set; } = new();

    public List<FlowRelationshipContext> Relationships { get; set; } = new();
}
