using System.Net.Http.Json;
using AiCodeAssistant.Domain.Entities;

namespace AiCodeAssistant.Client.Services.Rest;

public class GraphRestService
{
    private readonly HttpClient _httpClient;

    public GraphRestService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<GraphNode>> GetNodesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<GraphNode>>("http://localhost:5217/api/graph/nodes")
               ?? new List<GraphNode>();
    }

    public async Task<List<GraphEdge>> GetEdgesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<GraphEdge>>("http://localhost:5217/api/graph/edges")
               ?? new List<GraphEdge>();
    }

    public async Task<List<CodeFlow>> GetFlowsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<CodeFlow>>("http://localhost:5217/api/graph/flows")
               ?? new List<CodeFlow>();
    }

    public async Task<List<EndpointInfo>> GetEndpointsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<EndpointInfo>>("http://localhost:5217/api/graph/endpoints")
               ?? new List<EndpointInfo>();
    }
}