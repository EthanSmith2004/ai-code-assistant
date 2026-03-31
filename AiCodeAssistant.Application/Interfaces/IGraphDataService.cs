using AiCodeAssistant.Domain.Entities;

namespace AiCodeAssistant.Application.Interfaces;

public interface IGraphDataService
{
    List<GraphNode> GetNodes();
    List<GraphEdge> GetEdges();
    List<CodeFlow> GetFlows();
    List<EndpointInfo> GetEndpoints();
}