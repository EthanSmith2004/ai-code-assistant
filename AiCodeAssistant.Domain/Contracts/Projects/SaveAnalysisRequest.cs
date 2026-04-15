namespace AiCodeAssistant.Domain.Contracts.Projects;

public class SaveAnalysisRequest
{
    public string Summary { get; set; } = string.Empty;

    public int FileCount { get; set; }

    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public int EndpointCount { get; set; }
}
