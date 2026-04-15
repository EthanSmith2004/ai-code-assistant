namespace AiCodeAssistant.Domain.Contracts.Projects;

public class DashboardSummaryDto
{
    public int TotalProjects { get; set; }

    public int TotalAnalyses { get; set; }

    public int TotalFilesAnalyzed { get; set; }

    public int TotalNodesAnalyzed { get; set; }

    public int TotalEdgesAnalyzed { get; set; }

    public int TotalEndpointsDiscovered { get; set; }

    public double AverageNodeCount { get; set; }

    public string LatestProjectName { get; set; } = string.Empty;

    public DateTime? LatestAnalysisAt { get; set; }
}
