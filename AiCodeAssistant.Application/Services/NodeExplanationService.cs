using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Contracts.Ai;

namespace AiCodeAssistant.Application.Services;

public class NodeExplanationService : INodeExplanationService
{
    public Task<ExplainNodeResponse> ExplainNodeAsync(ExplainNodeRequest request)
    {
        var node = request.SelectedNode;
        var explanation = string.Join("\n", new[]
        {
            $"{node.Label} is a {HumanizeIdentifier(node.Type)} in the {HumanizeIdentifier(node.Layer)} layer.",
            CleanDescription(node.Description),
            FormatNodeRelationships(request.IncomingRelationships, "It is reached from", "No incoming relationships are mapped yet."),
            FormatNodeRelationships(request.OutgoingRelationships, "From here it works with", "No outgoing relationships are mapped yet."),
            "Why it matters: this node is one of the points where responsibility changes hands in the codebase."
        }.Where(line => !string.IsNullOrWhiteSpace(line)));

        return Task.FromResult(new ExplainNodeResponse
        {
            Explanation = explanation
        });
    }

    public Task<ExplainFlowResponse> ExplainFlowAsync(ExplainFlowRequest request)
    {
        var orderedNodes = request.OrderedNodes
            .OrderBy(flowNode => flowNode.Position)
            .Select(flowNode => flowNode.Node.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        var explanation = string.Join("\n", new[]
        {
            $"{request.FlowName} shows the likely path through the code.",
            FormatPath("Path", orderedNodes),
            CleanDescription(request.Description),
            FormatFlowRelationships(request.Relationships),
            "Why it matters: this view helps you see which pieces need to cooperate for the behavior to work."
        }.Where(line => !string.IsNullOrWhiteSpace(line)));

        return Task.FromResult(new ExplainFlowResponse
        {
            Explanation = explanation
        });
    }

    public Task<ExplainEndpointResponse> ExplainEndpointAsync(ExplainEndpointRequest request)
    {
        var endpoint = request.Endpoint;
        var pathNodes = request.InferredPath
            .OrderBy(flowNode => flowNode.Position)
            .Select(flowNode => flowNode.Node.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        var requestType = FormatEndpointType(endpoint.RequestType, "no request body", "an unspecified request body");
        var responseType = FormatEndpointType(endpoint.ResponseType, "no response body", "an unspecified response");
        var explanation = string.Join("\n", new[]
        {
            $"{endpoint.HttpMethod} {endpoint.Route} is an entry point into the API.",
            $"It takes {requestType} and returns {responseType}.",
            FormatPath("Focused path", pathNodes),
            FormatFlowRelationships(request.Relationships),
            "Why it matters: this is where an outside request starts before the work moves into controllers, services, or data access."
        }.Where(line => !string.IsNullOrWhiteSpace(line)));

        return Task.FromResult(new ExplainEndpointResponse
        {
            Explanation = explanation
        });
    }

    private static string FormatNodeRelationships(
        List<NodeRelationshipContext> relationships,
        string prefix,
        string emptyText)
    {
        if (relationships.Count == 0)
        {
            return emptyText;
        }

        var relatedNodes = relationships
            .Take(4)
            .Select(relationship => $"{relationship.Node.Label} via {HumanizeIdentifier(relationship.Relationship)}")
            .ToList();
        var extraCount = relationships.Count - relatedNodes.Count;
        var suffix = extraCount > 0 ? $" and {extraCount} more" : string.Empty;

        return $"{prefix} {string.Join(", ", relatedNodes)}{suffix}.";
    }

    private static string FormatFlowRelationships(List<FlowRelationshipContext> relationships)
    {
        if (relationships.Count == 0)
        {
            return "No direct code relationships are mapped between these steps yet.";
        }

        var steps = relationships
            .Take(5)
            .Select(relationship =>
                $"{relationship.SourceNode.Label} -> {relationship.TargetNode.Label} ({HumanizeIdentifier(relationship.Relationship)})")
            .ToList();
        var extraCount = relationships.Count - steps.Count;
        var suffix = extraCount > 0 ? $" and {extraCount} more" : string.Empty;

        return $"Mapped links: {string.Join(", ", steps)}{suffix}.";
    }

    private static string FormatPath(string label, List<string> nodes)
    {
        return nodes.Count == 0
            ? $"{label}: not enough path data is available yet."
            : $"{label}: {string.Join(" -> ", nodes)}.";
    }

    private static string CleanDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        return description.Trim();
    }

    private static string FormatEndpointType(string typeName, string noneText, string unknownText)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return unknownText;
        }

        if (string.Equals(typeName, "None", StringComparison.OrdinalIgnoreCase))
        {
            return noneText;
        }

        if (string.Equals(typeName, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return unknownText;
        }

        return typeName;
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var spaced = string.Empty;

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            var previous = index > 0 ? value[index - 1] : '\0';

            if (index > 0 &&
                char.IsUpper(current) &&
                (char.IsLower(previous) || char.IsDigit(previous)))
            {
                spaced += ' ';
            }

            spaced += current is '-' or '_' ? ' ' : current;
        }

        return spaced.Trim().ToLowerInvariant();
    }
}
