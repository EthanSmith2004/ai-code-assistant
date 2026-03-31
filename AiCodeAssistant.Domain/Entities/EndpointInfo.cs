namespace AiCodeAssistant.Domain.Entities;

public class EndpointInfo
{
    public string NodeId { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public string RequestType { get; set; } = string.Empty;

    public string ResponseType { get; set; } = string.Empty;
}