namespace AiCodeAssistant.Domain.Entities;

public class GraphEdge
{
    public string Id { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public RelationshipType Relationship { get; set; }
    // Examples: Calls, Uses, MapsTo, ReadsFrom, WritesTo, Queries, Returns
}