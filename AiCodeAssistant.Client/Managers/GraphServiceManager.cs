using AiCodeAssistant.Client.Services.Rest;
using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Contracts.Ai;
using AiCodeAssistant.Domain.Contracts.Projects;
using AiCodeAssistant.Domain.Entities;
using AiCodeAssistant.Domain.Graph;

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

    public async Task<CodeGraph> ScanSimplifiedAsync(ProjectScanRequest request)
    {
        return await _graphRestService.ScanSimplifiedAsync(request);
    }

    public async Task<ExplainNodeResponse> ExplainNodeAsync(ExplainNodeRequest request)
    {
        return await _graphRestService.ExplainNodeAsync(request);
    }

    public async Task<ExplainFlowResponse> ExplainFlowAsync(ExplainFlowRequest request)
    {
        return await _graphRestService.ExplainFlowAsync(request);
    }

    public async Task<ExplainEndpointResponse> ExplainEndpointAsync(ExplainEndpointRequest request)
    {
        return await _graphRestService.ExplainEndpointAsync(request);
    }

    public async Task<List<ProjectDto>> GetProjectsAsync()
    {
        return await _graphRestService.GetProjectsAsync();
    }

    public async Task<List<ProjectAnalysisDto>> GetProjectAnalysesAsync(Guid projectId)
    {
        return await _graphRestService.GetProjectAnalysesAsync(projectId);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
    {
        return await _graphRestService.GetDashboardSummaryAsync();
    }

    public async Task<ProjectDto> SaveProjectAsync(SaveProjectRequest request)
    {
        return await _graphRestService.SaveProjectAsync(request);
    }

    public async Task<ProjectAnalysisDto> SaveAnalysisAsync(Guid projectId, SaveAnalysisRequest request)
    {
        return await _graphRestService.SaveAnalysisAsync(projectId, request);
    }
}
