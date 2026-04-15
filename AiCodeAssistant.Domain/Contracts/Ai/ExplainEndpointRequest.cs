using AiCodeAssistant.Domain.Entities;

namespace AiCodeAssistant.Domain.Contracts.Ai;

public class ExplainEndpointRequest
{
    public EndpointInfo Endpoint { get; set; } = new();

    public List<FlowNodeContext> InferredPath { get; set; } = new();

    public List<FlowRelationshipContext> Relationships { get; set; } = new();
}
