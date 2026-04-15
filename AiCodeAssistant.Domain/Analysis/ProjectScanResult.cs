namespace AiCodeAssistant.Domain.Analysis;

public class ProjectScanResult
{
    public string RootPath { get; set; } = string.Empty;

    public List<string> FilePaths { get; set; } = new();
}
