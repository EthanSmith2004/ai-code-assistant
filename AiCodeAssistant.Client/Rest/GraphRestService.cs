using System.Net;
using System.Net.Http.Json;
using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Contracts.Ai;
using AiCodeAssistant.Domain.Contracts.Projects;
using AiCodeAssistant.Domain.Entities;
using AiCodeAssistant.Domain.Graph;

namespace AiCodeAssistant.Client.Services.Rest;

public class GraphRestService
{
    private const string ApiBaseUrl = "http://localhost:5217";
    private readonly HttpClient _httpClient;

    public GraphRestService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<GraphNode>> GetNodesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<GraphNode>>($"{ApiBaseUrl}/api/graph/nodes")
               ?? new List<GraphNode>();
    }

    public async Task<List<GraphEdge>> GetEdgesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<GraphEdge>>($"{ApiBaseUrl}/api/graph/edges")
               ?? new List<GraphEdge>();
    }

    public async Task<List<CodeFlow>> GetFlowsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<CodeFlow>>($"{ApiBaseUrl}/api/graph/flows")
               ?? new List<CodeFlow>();
    }

    public async Task<List<EndpointInfo>> GetEndpointsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<EndpointInfo>>($"{ApiBaseUrl}/api/graph/endpoints")
               ?? new List<EndpointInfo>();
    }

    public async Task<CodeGraph> ScanSimplifiedAsync(ProjectScanRequest request)
    {
        return await PostForJsonAsync<ProjectScanRequest, CodeGraph>(
            $"{ApiBaseUrl}/api/analysis/scan/simplified",
            request,
            "simplified project scan");
    }

    public async Task<ExplainNodeResponse> ExplainNodeAsync(ExplainNodeRequest request)
    {
        return await PostForJsonAsync<ExplainNodeRequest, ExplainNodeResponse>(
            $"{ApiBaseUrl}/api/ai/explain-node",
            request,
            "node explanation");
    }

    public async Task<ExplainFlowResponse> ExplainFlowAsync(ExplainFlowRequest request)
    {
        return await PostForJsonAsync<ExplainFlowRequest, ExplainFlowResponse>(
            $"{ApiBaseUrl}/api/ai/explain-flow",
            request,
            "flow explanation");
    }

    public async Task<ExplainEndpointResponse> ExplainEndpointAsync(ExplainEndpointRequest request)
    {
        return await PostForJsonAsync<ExplainEndpointRequest, ExplainEndpointResponse>(
            $"{ApiBaseUrl}/api/ai/explain-endpoint",
            request,
            "endpoint explanation");
    }

    public async Task<List<ProjectDto>> GetProjectsAsync()
    {
        return await GetForJsonAsync<List<ProjectDto>>(
            $"{ApiBaseUrl}/api/projects",
            "saved projects");
    }

    public async Task<List<ProjectAnalysisDto>> GetProjectAnalysesAsync(Guid projectId)
    {
        return await GetForJsonAsync<List<ProjectAnalysisDto>>(
            $"{ApiBaseUrl}/api/projects/{projectId}/analyses",
            "project analyses");
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
    {
        return await GetForJsonAsync<DashboardSummaryDto>(
            $"{ApiBaseUrl}/api/dashboard/summary",
            "dashboard summary");
    }

    public async Task<ProjectDto> SaveProjectAsync(SaveProjectRequest request)
    {
        return await PostForJsonAsync<SaveProjectRequest, ProjectDto>(
            $"{ApiBaseUrl}/api/projects",
            request,
            "project save");
    }

    public async Task<ProjectAnalysisDto> SaveAnalysisAsync(Guid projectId, SaveAnalysisRequest request)
    {
        return await PostForJsonAsync<SaveAnalysisRequest, ProjectAnalysisDto>(
            $"{ApiBaseUrl}/api/projects/{projectId}/analyses",
            request,
            "analysis save");
    }

    private async Task<TResponse> GetForJsonAsync<TResponse>(string url, string operation)
        where TResponse : new()
    {
        try
        {
            var response = await _httpClient.GetAsync(url);

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
                $"Could not reach the API for {operation}. Make sure the API is running at {ApiBaseUrl}.",
                exception);
        }
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
                $"Could not reach the API for {operation}. Make sure the API is running at {ApiBaseUrl}.",
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
