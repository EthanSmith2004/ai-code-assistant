using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Entities;
using AiCodeAssistant.Domain.Graph;

namespace AiCodeAssistant.Application.Services;

public class CodeGraphSimplifier : ICodeGraphSimplifier
{
    private const int MinimumImportance = 45;

    public CodeGraph Simplify(CodeGraph graph)
    {
        var importantNodeIds = graph.Nodes
            .Where(node => !IsNoise(node))
            .Where(node => GetImportanceScore(node) >= MinimumImportance)
            .Select(node => node.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        PreserveKeyRelationshipNodes(graph, importantNodeIds);

        var nodes = graph.Nodes
            .Where(node => importantNodeIds.Contains(node.Id))
            .ToList();

        var edges = graph.Edges
            .Where(edge => IsKeyRelationship(edge) || edge.Relationship == "Contains")
            .Where(edge => importantNodeIds.Contains(edge.SourceId) && importantNodeIds.Contains(edge.TargetId))
            .ToList();
        var endpoints = graph.Endpoints
            .Where(endpoint => importantNodeIds.Contains(endpoint.NodeId))
            .ToList();
        var flows = graph.Flows
            .Select(flow => new CodeFlow
            {
                Id = flow.Id,
                Name = flow.Name,
                Description = flow.Description,
                NodeIds = flow.NodeIds
                    .Where(importantNodeIds.Contains)
                    .ToList()
            })
            .Where(flow => flow.NodeIds.Count >= 2)
            .ToList();

        return new CodeGraph
        {
            Nodes = nodes,
            Edges = edges,
            Endpoints = endpoints,
            Flows = flows,
            DetectedFrameworks = graph.DetectedFrameworks
        };
    }

    private static void PreserveKeyRelationshipNodes(CodeGraph graph, HashSet<string> importantNodeIds)
    {
        foreach (var edge in graph.Edges.Where(IsKeyRelationship))
        {
            importantNodeIds.Add(edge.SourceId);
            importantNodeIds.Add(edge.TargetId);
        }
    }

    private static bool IsKeyRelationship(CodeEdge edge)
    {
        return edge.Relationship is "Uses" or "MapsTo" or "HasEntryPoint" or "ContainsProjectFile" or "ContainsConfig";
    }

    private static int GetImportanceScore(CodeNode node)
    {
        return node.Kind switch
        {
            "Controller" => 100,
            "Endpoint" => 98,
            "Route" => 95,
            "Service" => 90,
            "Manager" => 88,
            "RestClient" => 84,
            "Analyzer" => 82,
            "Scanner" => 80,
            "Detector" => 80,
            "Simplifier" => 80,
            "Repository" => 78,
            "EntryPoint" => 70,
            "Component" => 55,
            "Entity" => 50,
            "Interface" => 45,
            "Project" => 45,
            "ProjectFile" => 45,
            "Config" => 45,
            "Folder" => 20,
            _ => 20
        };
    }

    private static bool IsNoise(CodeNode node)
    {
        var sourcePath = node.SourcePath.Replace('\\', '/');
        var fileName = Path.GetFileName(sourcePath);

        return sourcePath.Contains("wwwroot/lib/", StringComparison.OrdinalIgnoreCase) ||
               sourcePath.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains(".min.", StringComparison.OrdinalIgnoreCase);
    }
}
