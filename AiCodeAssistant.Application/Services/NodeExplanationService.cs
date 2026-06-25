using System.Text;
using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Contracts.Ai;

namespace AiCodeAssistant.Application.Services;

/// <summary>
/// Produces natural-language explanations of graph nodes, flows, and endpoints.
/// When an LLM provider is configured it generates a real explanation from the
/// structured context; otherwise (or if the provider call fails) it falls back
/// to a deterministic template so the feature always returns something useful.
/// </summary>
public class NodeExplanationService : INodeExplanationService
{
    private const string SystemPrompt =
        "You are a senior software engineer helping another engineer understand an unfamiliar codebase. " +
        "You are given structured facts about one part of a code architecture graph. " +
        "Explain it clearly and specifically, grounded only in the facts provided - never invent files, " +
        "technologies, or relationships that are not stated. Cover what it is, the responsibility it holds, " +
        "how it connects to the rest of the system, and why it matters. " +
        "Write in plain prose with short paragraphs separated by blank lines. " +
        "Do not use markdown, headings, bullet points, asterisks, or backticks. Keep it to 3-6 sentences.";

    private readonly IChatCompletionClient _chatClient;

    public NodeExplanationService(IChatCompletionClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<ExplainNodeResponse> ExplainNodeAsync(ExplainNodeRequest request)
    {
        var generated = await _chatClient.CompleteAsync(new ChatPrompt
        {
            SystemPrompt = SystemPrompt,
            UserPrompt = BuildNodePrompt(request)
        });

        return new ExplainNodeResponse
        {
            Explanation = generated ?? BuildNodeFallback(request)
        };
    }

    public async Task<ExplainFlowResponse> ExplainFlowAsync(ExplainFlowRequest request)
    {
        var generated = await _chatClient.CompleteAsync(new ChatPrompt
        {
            SystemPrompt = SystemPrompt,
            UserPrompt = BuildFlowPrompt(request)
        });

        return new ExplainFlowResponse
        {
            Explanation = generated ?? BuildFlowFallback(request)
        };
    }

    public async Task<ExplainEndpointResponse> ExplainEndpointAsync(ExplainEndpointRequest request)
    {
        var generated = await _chatClient.CompleteAsync(new ChatPrompt
        {
            SystemPrompt = SystemPrompt,
            UserPrompt = BuildEndpointPrompt(request)
        });

        return new ExplainEndpointResponse
        {
            Explanation = generated ?? BuildEndpointFallback(request)
        };
    }

    // ----- Prompt building (structured facts handed to the model) -----

    private static string BuildNodePrompt(ExplainNodeRequest request)
    {
        var node = request.SelectedNode;
        var builder = new StringBuilder();

        builder.AppendLine("Explain this element of the code architecture graph.");
        builder.AppendLine();
        builder.AppendLine($"Name: {Fallback(node.Label, "(unnamed)")}");
        builder.AppendLine($"Type: {HumanizeIdentifier(node.Type)}");
        builder.AppendLine($"Architecture layer: {HumanizeIdentifier(node.Layer)}");
        builder.AppendLine($"Description: {Fallback(CleanDescription(node.Description), "(none provided)")}");
        builder.AppendLine();
        AppendNodeRelationships(builder, "Incoming relationships (what reaches or depends on this):", request.IncomingRelationships);
        builder.AppendLine();
        AppendNodeRelationships(builder, "Outgoing relationships (what this reaches or depends on):", request.OutgoingRelationships);

        return builder.ToString().TrimEnd();
    }

    private static string BuildFlowPrompt(ExplainFlowRequest request)
    {
        var orderedNodes = request.OrderedNodes
            .OrderBy(flowNode => flowNode.Position)
            .Select(flowNode => flowNode.Node.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("Explain this flow (an inferred path through the code).");
        builder.AppendLine();
        builder.AppendLine($"Flow name: {Fallback(request.FlowName, "(unnamed flow)")}");
        builder.AppendLine($"Description: {Fallback(CleanDescription(request.Description), "(none provided)")}");
        builder.AppendLine();
        builder.AppendLine(orderedNodes.Count == 0
            ? "Ordered path: (not enough path data)"
            : $"Ordered path: {string.Join(" -> ", orderedNodes)}");
        builder.AppendLine();
        AppendFlowRelationships(builder, request.Relationships);

        return builder.ToString().TrimEnd();
    }

    private static string BuildEndpointPrompt(ExplainEndpointRequest request)
    {
        var endpoint = request.Endpoint;
        var pathNodes = request.InferredPath
            .OrderBy(flowNode => flowNode.Position)
            .Select(flowNode => flowNode.Node.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("Explain this API endpoint and what happens when it is called.");
        builder.AppendLine();
        builder.AppendLine($"Endpoint: {endpoint.HttpMethod} {endpoint.Route}");
        builder.AppendLine($"Request body type: {FormatEndpointType(endpoint.RequestType, "none", "unspecified")}");
        builder.AppendLine($"Response type: {FormatEndpointType(endpoint.ResponseType, "none", "unspecified")}");
        builder.AppendLine();
        builder.AppendLine(pathNodes.Count == 0
            ? "Inferred path: (not enough path data)"
            : $"Inferred path: {string.Join(" -> ", pathNodes)}");
        builder.AppendLine();
        AppendFlowRelationships(builder, request.Relationships);

        return builder.ToString().TrimEnd();
    }

    private static void AppendNodeRelationships(StringBuilder builder, string header, List<NodeRelationshipContext> relationships)
    {
        builder.AppendLine(header);
        if (relationships.Count == 0)
        {
            builder.AppendLine("- (none mapped)");
            return;
        }

        foreach (var relationship in relationships.Take(12))
        {
            builder.AppendLine($"- {Fallback(relationship.Node.Label, "(unnamed)")} ({HumanizeIdentifier(relationship.Relationship)})");
        }

        var extra = relationships.Count - Math.Min(relationships.Count, 12);
        if (extra > 0)
        {
            builder.AppendLine($"- ...and {extra} more");
        }
    }

    private static void AppendFlowRelationships(StringBuilder builder, List<FlowRelationshipContext> relationships)
    {
        builder.AppendLine("Mapped relationships between steps:");
        if (relationships.Count == 0)
        {
            builder.AppendLine("- (none mapped)");
            return;
        }

        foreach (var relationship in relationships.Take(12))
        {
            builder.AppendLine(
                $"- {Fallback(relationship.SourceNode.Label, "(unnamed)")} -> {Fallback(relationship.TargetNode.Label, "(unnamed)")} ({HumanizeIdentifier(relationship.Relationship)})");
        }

        var extra = relationships.Count - Math.Min(relationships.Count, 12);
        if (extra > 0)
        {
            builder.AppendLine($"- ...and {extra} more");
        }
    }

    private static string Fallback(string value, string whenEmpty)
    {
        return string.IsNullOrWhiteSpace(value) ? whenEmpty : value.Trim();
    }

    // ----- Deterministic fallbacks (used when no LLM is configured) -----

    private static string BuildNodeFallback(ExplainNodeRequest request)
    {
        var node = request.SelectedNode;
        return string.Join("\n", new[]
        {
            $"{node.Label} is a {HumanizeIdentifier(node.Type)} in the {HumanizeIdentifier(node.Layer)} layer.",
            CleanDescription(node.Description),
            FormatNodeRelationships(request.IncomingRelationships, "It is reached from", "No incoming relationships are mapped yet."),
            FormatNodeRelationships(request.OutgoingRelationships, "From here it works with", "No outgoing relationships are mapped yet."),
            "Why it matters: this node is one of the points where responsibility changes hands in the codebase."
        }.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string BuildFlowFallback(ExplainFlowRequest request)
    {
        var orderedNodes = request.OrderedNodes
            .OrderBy(flowNode => flowNode.Position)
            .Select(flowNode => flowNode.Node.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        return string.Join("\n", new[]
        {
            $"{request.FlowName} shows the likely path through the code.",
            FormatPath("Path", orderedNodes),
            CleanDescription(request.Description),
            FormatFlowRelationships(request.Relationships),
            "Why it matters: this view helps you see which pieces need to cooperate for the behavior to work."
        }.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string BuildEndpointFallback(ExplainEndpointRequest request)
    {
        var endpoint = request.Endpoint;
        var pathNodes = request.InferredPath
            .OrderBy(flowNode => flowNode.Position)
            .Select(flowNode => flowNode.Node.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        var requestType = FormatEndpointType(endpoint.RequestType, "no request body", "an unspecified request body");
        var responseType = FormatEndpointType(endpoint.ResponseType, "no response body", "an unspecified response");
        return string.Join("\n", new[]
        {
            $"{endpoint.HttpMethod} {endpoint.Route} is an entry point into the API.",
            $"It takes {requestType} and returns {responseType}.",
            FormatPath("Focused path", pathNodes),
            FormatFlowRelationships(request.Relationships),
            "Why it matters: this is where an outside request starts before the work moves into controllers, services, or data access."
        }.Where(line => !string.IsNullOrWhiteSpace(line)));
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
