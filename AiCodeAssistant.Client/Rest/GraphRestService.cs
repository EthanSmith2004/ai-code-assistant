using System.Net;
using System.Net.Http.Json;
using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Contracts.Ai;
using AiCodeAssistant.Domain.Entities;
using AiCodeAssistant.Domain.Graph;

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

    public async Task<CodeGraph> ScanSimplifiedAsync(ProjectScanRequest request)
    {
        return await PostForJsonAsync<ProjectScanRequest, CodeGraph>(
            "http://localhost:5217/api/analysis/scan/simplified",
            request,
            "simplified project scan");
    }

    public async Task<ExplainNodeResponse> ExplainNodeAsync(ExplainNodeRequest request)
    {
        return await PostForJsonAsync<ExplainNodeRequest, ExplainNodeResponse>(
            "http://localhost:5217/api/ai/explain-node",
            request,
            "node explanation");
    }

    public async Task<ExplainFlowResponse> ExplainFlowAsync(ExplainFlowRequest request)
    {
        return await PostForJsonAsync<ExplainFlowRequest, ExplainFlowResponse>(
            "http://localhost:5217/api/ai/explain-flow",
            request,
            "flow explanation");
    }

    public async Task<ExplainEndpointResponse> ExplainEndpointAsync(ExplainEndpointRequest request)
    {
        return await PostForJsonAsync<ExplainEndpointRequest, ExplainEndpointResponse>(
            "http://localhost:5217/api/ai/explain-endpoint",
            request,
            "endpoint explanation");
    }

    private async Task<TResponse> PostForJsonAsync<TRequest, TResponse>(
        string url,
        TRequest request,
        string operation)
        where TResponse : new()
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(CreateApiErrorMessage(operation, response.StatusCode, errorBody));
            }

            return await response.Content.ReadFromJsonAsync<TResponse>()
                   ?? new TResponse();
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                $"Could not reach the API for {operation}. Make sure the API is running at http://localhost:5217.",
                exception);
        }
    }

    private static string CreateApiErrorMessage(string operation, HttpStatusCode statusCode, string errorBody)
    {
        if (statusCode == HttpStatusCode.NotFound)
        {
            return $"The API endpoint for {operation} was not found. Restart the API so the latest endpoint is loaded.";
        }

        var status = $"{(int)statusCode} {statusCode}";

        return string.IsNullOrWhiteSpace(errorBody)
            ? $"The API returned {status} while requesting {operation}."
            : $"The API returned {status} while requesting {operation}: {errorBody}";
    }
}
