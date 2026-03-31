namespace AiCodeAssistant.Domain.Entities;

public class GraphNode
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public NodeType Type { get; set; }
    // Examples: Page, Endpoint, Controller, Service, Repository, Database

    public LayerType Layer { get; set; }
    // Examples: Frontend, API, Application, Data

    public string Description { get; set; } = string.Empty;
}