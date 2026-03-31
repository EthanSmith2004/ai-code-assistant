namespace AiCodeAssistant.Domain.Entities;

public enum NodeType
{
    Page,
    Component,
    Endpoint,
    Controller,
    Service,
    Repository,
    Database,
    ExternalApi
}

public enum LayerType
{
    Frontend,
    Api,
    Application,
    Data
}

public enum RelationshipType
{
    Calls,
    Uses,
    MapsTo,
    ReadsFrom,
    WritesTo,
    Queries,
    Returns
}