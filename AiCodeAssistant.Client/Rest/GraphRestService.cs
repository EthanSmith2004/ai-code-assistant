using System.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;
using AiCodeAssistant.Client.Services;
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
    private readonly AuthService _authService;

    public GraphRestService(HttpClient httpClient, AuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<List<GraphNode>> GetNodesAsync()
    {
        return await GetForJsonAsync<List<GraphNode>>(
            $"{ApiBaseUrl}/api/graph/nodes",
            "graph nodes");
    }

    public async Task<List<GraphEdge>> GetEdgesAsync()
    {
        return await GetForJsonAsync<List<GraphEdge>>(
            $"{ApiBaseUrl}/api/graph/edges",
            "graph edges");
    }

    public async Task<List<CodeFlow>> GetFlowsAsync()
    {
        return await GetForJsonAsync<List<CodeFlow>>(
            $"{ApiBaseUrl}/api/graph/flows",
            "graph flows");
    }

    public async Task<List<EndpointInfo>> GetEndpointsAsync()
    {
        return await GetForJsonAsync<List<EndpointInfo>>(
            $"{ApiBaseUrl}/api/graph/endpoints",
            "graph endpoints");
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
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            await AttachAuthorizationHeaderAsync(request);
            var response = await _httpClient.SendAsync(request);

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
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request)
            };
            await AttachAuthorizationHeaderAsync(httpRequest);
            var response = await _httpClient.SendAsync(httpRequest);

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

    private async Task AttachAuthorizationHeaderAsync(HttpRequestMessage request)
    {
        var token = await _authService.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static string CreateApiErrorMessage(string operation, HttpStatusCode statusCode, string errorBody)
    {
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            return "Please log in to continue. If you already logged in, your session may have expired.";
        }

        if (statusCode == HttpStatusCode.Forbidden)
        {
            return "You do not have access to that CodeSight data.";
        }

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
