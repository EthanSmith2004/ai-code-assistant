namespace AiCodeAssistant.Domain.Entities;

public class GraphNode
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;
    // Examples: Page, Endpoint, Controller, Service, Repository, Database

    public string Layer { get; set; } = string.Empty;
    // Examples: Frontend, API, Application, Data

    public string Description { get; set; } = string.Empty;
}