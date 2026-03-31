using AiCodeAssistant.Client.Services.Rest;
using AiCodeAssistant.Domain.Entities;

namespace AiCodeAssistant.Client.Services.Managers;

public class GraphServiceManager
{
    private readonly GraphRestService _graphRestService;

    public GraphServiceManager(GraphRestService graphRestService)
    {
        _graphRestService = graphRestService;
    }

    public async Task<List<GraphNode>> GetNodesAsync()
    {
        return await _graphRestService.GetNodesAsync();
    }

    public async Task<List<GraphEdge>> GetEdgesAsync()
    {
        return await _graphRestService.GetEdgesAsync();
    }

    public async Task<List<CodeFlow>> GetFlowsAsync()
    {
        return await _graphRestService.GetFlowsAsync();
    }

    public async Task<List<EndpointInfo>> GetEndpointsAsync()
    {
        return await _graphRestService.GetEndpointsAsync();
    }
}