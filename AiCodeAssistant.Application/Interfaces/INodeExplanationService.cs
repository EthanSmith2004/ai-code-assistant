using AiCodeAssistant.Domain.Contracts.Ai;

namespace AiCodeAssistant.Application.Interfaces;

public interface INodeExplanationService
{
    Task<ExplainNodeResponse> ExplainNodeAsync(ExplainNodeRequest request);

    Task<ExplainFlowResponse> ExplainFlowAsync(ExplainFlowRequest request);

    Task<ExplainEndpointResponse> ExplainEndpointAsync(ExplainEndpointRequest request);
}
