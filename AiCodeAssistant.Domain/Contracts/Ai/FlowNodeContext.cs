namespace AiCodeAssistant.Domain.Contracts.Ai;

public class FlowNodeContext
{
    public int Position { get; set; }

    public NodeContext Node { get; set; } = new();
}
