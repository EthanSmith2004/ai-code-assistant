namespace AiCodeAssistant.Domain.Analysis;

public class FrameworkDetectionResult
{
    public string Language { get; set; } = string.Empty;

    public string Framework { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public List<string> Evidence { get; set; } = new();
}
