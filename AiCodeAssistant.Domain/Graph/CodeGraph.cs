using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Entities;

namespace AiCodeAssistant.Domain.Graph;

public class CodeGraph
{
    public List<CodeNode> Nodes { get; set; } = new();

    public List<CodeEdge> Edges { get; set; } = new();

    public List<EndpointInfo> Endpoints { get; set; } = new();

    public List<CodeFlow> Flows { get; set; } = new();

    public List<FrameworkDetectionResult> DetectedFrameworks { get; set; } = new();
}
