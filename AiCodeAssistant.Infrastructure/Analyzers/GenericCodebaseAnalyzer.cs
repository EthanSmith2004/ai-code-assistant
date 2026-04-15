using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Entities;
using AiCodeAssistant.Domain.Graph;
using System.Text.RegularExpressions;

namespace AiCodeAssistant.Infrastructure.Analyzers;

public class GenericCodebaseAnalyzer : ICodebaseAnalyzer
{
    private static readonly Regex HttpAttributeRegex = new(
        @"^\s*\[Http(?<verb>Get|Post|Put|Delete|Patch)(?:\s*\(\s*""(?<route>[^""]*)""\s*\))?\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RouteAttributeRegex = new(
        @"^\s*\[Route\s*\(\s*""(?<route>[^""]*)""\s*\)\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ControllerClassRegex = new(
        @"\bclass\s+(?<name>\w+Controller)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ActionMethodRegex = new(
        @"^\s*public\s+(?:async\s+)?(?<return>[\w<>\[\],\s\.\?]+)\s+(?<name>\w+)\s*\((?<parameters>[^)]*)\)",
        RegexOptions.CultureInvariant);

    public Task<CodeGraph> AnalyzeAsync(
        ProjectScanResult scanResult,
        IReadOnlyList<FrameworkDetectionResult> detections,
        CancellationToken cancellationToken = default)
    {
        var graph = new CodeGraph
        {
            DetectedFrameworks = detections.ToList()
        };

        var rootNode = new CodeNode
        {
            Id = "project-root",
            Label = Path.GetFileName(scanResult.RootPath),
            Kind = "Project",
            Layer = "Workspace",
            SourcePath = scanResult.RootPath,
            Description = "Root folder for the scanned project."
        };

        graph.Nodes.Add(rootNode);

        var folderNodesByPath = new Dictionary<string, CodeNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in scanResult.FilePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = NormalizePath(filePath);
            var parentNode = EnsureFolderNodes(graph, rootNode, normalizedPath, folderNodesByPath, detections);
            var fileNode = CreateFileNode(normalizedPath, detections);

            graph.Nodes.Add(fileNode);
            AddEdge(graph, parentNode.Id, fileNode.Id, "Contains");
            AddProjectRelationship(graph, rootNode, fileNode);
        }

        AddNameBasedRelationships(graph);
        AddSourceTextRelationships(graph, scanResult, cancellationToken);
        AddAspNetCoreEndpoints(graph, scanResult, cancellationToken);
        AddInferredFlows(graph);

        return Task.FromResult(graph);
    }

    private static CodeNode EnsureFolderNodes(
        CodeGraph graph,
        CodeNode rootNode,
        string filePath,
        Dictionary<string, CodeNode> folderNodesByPath,
        IReadOnlyList<FrameworkDetectionResult> detections)
    {
        var folderPath = Path.GetDirectoryName(filePath)?.Replace('\\', '/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return rootNode;
        }

        var parentNode = rootNode;
        var currentPath = string.Empty;

        foreach (var segment in folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = string.IsNullOrWhiteSpace(currentPath)
                ? segment
                : $"{currentPath}/{segment}";

            if (!folderNodesByPath.TryGetValue(currentPath, out var folderNode))
            {
                folderNode = new CodeNode
                {
                    Id = CreateNodeId("folder", currentPath),
                    Label = segment,
                    Kind = "Folder",
                    Layer = "Workspace",
                    Framework = GetFramework(currentPath, detections),
                    SourcePath = currentPath,
                    Description = $"Folder discovered during project scan: {currentPath}"
                };

                folderNodesByPath[currentPath] = folderNode;
                graph.Nodes.Add(folderNode);
            }

            AddEdge(graph, parentNode.Id, folderNode.Id, "Contains");
            parentNode = folderNode;
        }

        return parentNode;
    }

    private static CodeNode CreateFileNode(string filePath, IReadOnlyList<FrameworkDetectionResult> detections)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);
        var kind = GetKind(filePath);

        return new CodeNode
        {
            Id = CreateNodeId("file", filePath),
            Label = fileName,
            Kind = kind,
            Layer = GetLayer(filePath),
            Language = GetLanguage(extension),
            Framework = GetFramework(filePath, detections),
            SourcePath = filePath,
            Description = CreateDescription(kind, filePath)
        };
    }

    private static void AddProjectRelationship(CodeGraph graph, CodeNode rootNode, CodeNode fileNode)
    {
        switch (fileNode.Kind)
        {
            case "Config":
                AddEdge(graph, rootNode.Id, fileNode.Id, "ContainsConfig");
                break;
            case "EntryPoint":
                AddEdge(graph, rootNode.Id, fileNode.Id, "HasEntryPoint");
                break;
            case "ProjectFile":
                AddEdge(graph, rootNode.Id, fileNode.Id, "ContainsProjectFile");
                break;
        }
    }

    private static void AddNameBasedRelationships(CodeGraph graph)
    {
        var controllers = graph.Nodes.Where(node => node.Kind == "Controller").ToList();
        var services = graph.Nodes.Where(node => node.Kind == "Service").ToList();
        var repositories = graph.Nodes.Where(node => node.Kind == "Repository").ToList();

        foreach (var controller in controllers)
        {
            var featureName = RemoveSuffix(Path.GetFileNameWithoutExtension(controller.Label), "Controller");
            var matchingServices = services.Where(service => HasMatchingFeatureName(service.Label, featureName));

            foreach (var service in matchingServices)
            {
                AddEdge(graph, controller.Id, service.Id, "Uses");
            }
        }

        foreach (var service in services)
        {
            var featureName = RemoveSuffix(Path.GetFileNameWithoutExtension(service.Label), "Service");
            var matchingRepositories = repositories.Where(repository => HasMatchingFeatureName(repository.Label, featureName));

            foreach (var repository in matchingRepositories)
            {
                AddEdge(graph, service.Id, repository.Id, "Uses");
            }
        }
    }

    private static void AddSourceTextRelationships(
        CodeGraph graph,
        ProjectScanResult scanResult,
        CancellationToken cancellationToken)
    {
        var sourceNodes = graph.Nodes
            .Where(IsBehavioralSourceNode)
            .Where(node => IsSourceTextFile(node.SourcePath))
            .ToList();
        var targetNodes = graph.Nodes
            .Where(IsBehavioralTargetNode)
            .ToList();
        var interfaceTargetsByName = GetSingleImplementationInterfaceTargets(graph, targetNodes);

        foreach (var sourceNode in sourceNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceText = TryReadSourceText(scanResult.RootPath, sourceNode.SourcePath);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                continue;
            }

            foreach (var targetNode in targetNodes)
            {
                if (sourceNode.Id == targetNode.Id)
                {
                    continue;
                }

                var targetTypeName = GetTypeName(targetNode);
                if (string.IsNullOrWhiteSpace(targetTypeName))
                {
                    continue;
                }

                if (IsTypeReferenced(sourceText, targetTypeName) ||
                    HasSingleImplementationInterfaceReference(sourceText, targetNode, interfaceTargetsByName))
                {
                    AddEdge(graph, sourceNode.Id, targetNode.Id, "Uses");
                }
            }
        }
    }

    private static void AddAspNetCoreEndpoints(
        CodeGraph graph,
        ProjectScanResult scanResult,
        CancellationToken cancellationToken)
    {
        var controllers = graph.Nodes
            .Where(node => node.Kind == "Controller")
            .Where(node => IsSourceTextFile(node.SourcePath))
            .ToList();

        foreach (var controller in controllers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceText = TryReadSourceText(scanResult.RootPath, controller.SourcePath);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                continue;
            }

            foreach (var endpoint in DetectControllerEndpoints(controller, sourceText))
            {
                var endpointNode = new CodeNode
                {
                    Id = endpoint.NodeId,
                    Label = $"{endpoint.HttpMethod} {endpoint.Route}",
                    Kind = "Endpoint",
                    Layer = "Api",
                    Language = "CSharp",
                    Framework = "ASP.NET Core",
                    SourcePath = controller.SourcePath,
                    Description = $"ASP.NET Core endpoint inferred from {controller.Label}.{endpoint.ActionName}."
                };

                if (graph.Nodes.All(node => node.Id != endpointNode.Id))
                {
                    graph.Nodes.Add(endpointNode);
                }

                if (graph.Endpoints.All(existingEndpoint => existingEndpoint.NodeId != endpointNode.Id))
                {
                    graph.Endpoints.Add(new EndpointInfo
                    {
                        NodeId = endpointNode.Id,
                        HttpMethod = endpoint.HttpMethod,
                        Route = endpoint.Route,
                        RequestType = endpoint.RequestType,
                        ResponseType = endpoint.ResponseType
                    });
                }

                AddEdge(graph, endpointNode.Id, controller.Id, "MapsTo");
            }
        }
    }

    private static IEnumerable<EndpointCandidate> DetectControllerEndpoints(CodeNode controller, string sourceText)
    {
        var lines = sourceText.Replace("\r\n", "\n").Split('\n');
        var controllerClassLineIndex = Array.FindIndex(lines, line => ControllerClassRegex.IsMatch(line));
        var controllerName = RemoveSuffix(GetTypeName(controller), "Controller");
        var classRoute = GetClassRoute(lines, controllerClassLineIndex, controllerName);
        string? pendingRoute = null;

        for (var index = Math.Max(controllerClassLineIndex + 1, 0); index < lines.Length; index++)
        {
            var routeMatch = RouteAttributeRegex.Match(lines[index]);
            if (routeMatch.Success)
            {
                pendingRoute = routeMatch.Groups["route"].Value;
                continue;
            }

            var httpMatch = HttpAttributeRegex.Match(lines[index]);
            if (!httpMatch.Success)
            {
                continue;
            }

            var httpMethod = httpMatch.Groups["verb"].Value.ToUpperInvariant();
            var methodRoute = httpMatch.Groups["route"].Success
                ? httpMatch.Groups["route"].Value
                : pendingRoute;
            var action = FindNextActionMethod(lines, index + 1, ref methodRoute);

            pendingRoute = null;

            if (action is null)
            {
                continue;
            }

            var route = BuildEndpointRoute(classRoute, methodRoute, controllerName, action.Name);
            if (string.IsNullOrWhiteSpace(route))
            {
                continue;
            }

            yield return new EndpointCandidate(
                CreateNodeId("endpoint", $"{controller.SourcePath}-{httpMethod}-{action.Name}-{route}"),
                httpMethod,
                route,
                action.Name,
                GetRequestType(action.Parameters),
                GetResponseType(action.ReturnType));
        }
    }

    private static string? GetClassRoute(string[] lines, int controllerClassLineIndex, string controllerName)
    {
        if (controllerClassLineIndex < 0)
        {
            return null;
        }

        var firstAttributeLine = Math.Max(0, controllerClassLineIndex - 10);
        for (var index = controllerClassLineIndex - 1; index >= firstAttributeLine; index--)
        {
            var routeMatch = RouteAttributeRegex.Match(lines[index]);
            if (routeMatch.Success)
            {
                return ApplyRouteTokens(routeMatch.Groups["route"].Value, controllerName, string.Empty);
            }
        }

        return null;
    }

    private static ActionCandidate? FindNextActionMethod(string[] lines, int startIndex, ref string? methodRoute)
    {
        var lastIndex = Math.Min(lines.Length, startIndex + 8);
        for (var index = startIndex; index < lastIndex; index++)
        {
            var routeMatch = RouteAttributeRegex.Match(lines[index]);
            if (routeMatch.Success && string.IsNullOrWhiteSpace(methodRoute))
            {
                methodRoute = routeMatch.Groups["route"].Value;
                continue;
            }

            var methodMatch = ActionMethodRegex.Match(lines[index]);
            if (methodMatch.Success)
            {
                return new ActionCandidate(
                    methodMatch.Groups["name"].Value,
                    methodMatch.Groups["return"].Value.Trim(),
                    methodMatch.Groups["parameters"].Value);
            }

            var trimmedLine = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("[", StringComparison.Ordinal))
            {
                continue;
            }

            break;
        }

        return null;
    }

    private static string BuildEndpointRoute(
        string? classRoute,
        string? methodRoute,
        string controllerName,
        string actionName)
    {
        var normalizedClassRoute = ApplyRouteTokens(classRoute, controllerName, actionName);
        var normalizedMethodRoute = ApplyRouteTokens(methodRoute, controllerName, actionName);

        if (string.IsNullOrWhiteSpace(normalizedClassRoute))
        {
            return NormalizeEndpointRoute(normalizedMethodRoute);
        }

        if (string.IsNullOrWhiteSpace(normalizedMethodRoute))
        {
            return NormalizeEndpointRoute(normalizedClassRoute);
        }

        if (normalizedMethodRoute.StartsWith("/", StringComparison.Ordinal) ||
            normalizedMethodRoute.StartsWith("~/", StringComparison.Ordinal))
        {
            return NormalizeEndpointRoute(normalizedMethodRoute);
        }

        return NormalizeEndpointRoute($"{normalizedClassRoute}/{normalizedMethodRoute}");
    }

    private static string ApplyRouteTokens(string? route, string controllerName, string actionName)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return string.Empty;
        }

        return route
            .Replace("[controller]", controllerName.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
            .Replace("[action]", actionName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEndpointRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return string.Empty;
        }

        var normalizedRoute = route
            .Replace("~/", "/", StringComparison.Ordinal)
            .Trim()
            .Trim('/');

        return string.IsNullOrWhiteSpace(normalizedRoute)
            ? "/"
            : $"/{normalizedRoute}";
    }

    private static string GetRequestType(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return "None";
        }

        foreach (var parameter in parameters.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var cleanedParameter = Regex.Replace(parameter, @"\[[^\]]+\]\s*", string.Empty).Trim();
            var parts = cleanedParameter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var typeName = parts.FirstOrDefault(part => !IsParameterModifier(part));

            if (!string.IsNullOrWhiteSpace(typeName) && !IsFrameworkParameterType(typeName))
            {
                return typeName;
            }
        }

        return "None";
    }

    private static string GetResponseType(string returnType)
    {
        var typeName = UnwrapReturnType(returnType.Trim());
        return typeName is "void" or "IActionResult" or "ActionResult" or "Task"
            ? "Unknown"
            : typeName;
    }

    private static string UnwrapReturnType(string returnType)
    {
        var unwrapped = TryUnwrapGeneric(returnType, "Task") ??
                        TryUnwrapGeneric(returnType, "ValueTask") ??
                        TryUnwrapGeneric(returnType, "ActionResult");

        return unwrapped is null ? returnType : UnwrapReturnType(unwrapped);
    }

    private static string? TryUnwrapGeneric(string value, string genericName)
    {
        var prefix = $"{genericName}<";
        if (!value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith(">", StringComparison.Ordinal))
        {
            return null;
        }

        return value[prefix.Length..^1].Trim();
    }

    private static bool IsParameterModifier(string value)
    {
        return value is "ref" or "out" or "in" or "params";
    }

    private static bool IsFrameworkParameterType(string typeName)
    {
        return typeName is "CancellationToken" or "HttpContext" or "ClaimsPrincipal" or "IFormFile" or "string"
            or "int" or "long" or "double" or "decimal" or "bool" or "Guid";
    }

    private static void AddInferredFlows(CodeGraph graph)
    {
        foreach (var endpoint in graph.Endpoints)
        {
            var nodeIds = new List<string> { endpoint.NodeId };
            var controllerId = graph.Edges
                .FirstOrDefault(edge => edge.SourceId == endpoint.NodeId && edge.Relationship == "MapsTo")
                ?.TargetId;

            if (string.IsNullOrWhiteSpace(controllerId))
            {
                continue;
            }

            AddFlowNode(nodeIds, controllerId);
            AddBehavioralFlowNodes(graph, controllerId, nodeIds, maxDepth: 3, maxNodeCount: 8);

            if (nodeIds.Count < 3)
            {
                continue;
            }

            graph.Flows.Add(new CodeFlow
            {
                Id = CreateNodeId("flow", endpoint.NodeId),
                Name = CreateFriendlyFlowName(endpoint),
                Description = $"Inferred from {endpoint.HttpMethod} {endpoint.Route}. Built from endpoint routing and high-confidence architectural relationships.",
                NodeIds = nodeIds
            });
        }
    }

    private static string CreateFriendlyFlowName(EndpointInfo endpoint)
    {
        var segments = GetIntentRouteSegments(endpoint.Route).ToList();
        var normalizedRoute = string.Join("/", segments).ToLowerInvariant();

        if (segments.Any(segment => segment.Equals("login", StringComparison.OrdinalIgnoreCase)))
        {
            return "Login Flow";
        }

        if (segments.Any(segment => segment.Equals("logout", StringComparison.OrdinalIgnoreCase)))
        {
            return "Logout Flow";
        }

        if (normalizedRoute.Contains("analysis", StringComparison.OrdinalIgnoreCase) &&
            normalizedRoute.Contains("scan", StringComparison.OrdinalIgnoreCase))
        {
            return "Analysis Scan Flow";
        }

        if (segments.Any(segment => segment.Equals("graph", StringComparison.OrdinalIgnoreCase)))
        {
            return "Graph Load Flow";
        }

        var explainSegmentIndex = segments.FindIndex(segment =>
            segment.StartsWith("explain", StringComparison.OrdinalIgnoreCase));

        if (explainSegmentIndex >= 0)
        {
            var explainSegment = segments[explainSegmentIndex];
            var target = explainSegment.Equals("explain", StringComparison.OrdinalIgnoreCase) &&
                         explainSegmentIndex + 1 < segments.Count
                ? segments[explainSegmentIndex + 1]
                : explainSegment["explain".Length..].Trim('-', '_', ' ');

            return string.IsNullOrWhiteSpace(target)
                ? "Explain Flow"
                : $"Explain {HumanizeRouteSegment(target)} Flow";
        }

        if (segments.Count == 0)
        {
            return $"{endpoint.HttpMethod} Endpoint Flow";
        }

        IEnumerable<string> titleSegments = segments.Count == 1
            ? segments
            : segments.TakeLast(2);

        return $"{string.Join(" ", titleSegments.Select(HumanizeRouteSegment))} Flow";
    }

    private static IEnumerable<string> GetIntentRouteSegments(string route)
    {
        return route
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim('{', '}', '?'))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Where(segment => !segment.Equals("api", StringComparison.OrdinalIgnoreCase))
            .Where(segment => !Regex.IsMatch(segment, @"^v\d+(\.\d+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Where(segment => !segment.Equals("id", StringComparison.OrdinalIgnoreCase));
    }

    private static string HumanizeRouteSegment(string segment)
    {
        var withoutPrefix = segment
            .Replace("get-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("post-", string.Empty, StringComparison.OrdinalIgnoreCase);
        var spaced = Regex.Replace(withoutPrefix, @"([a-z0-9])([A-Z])", "$1 $2");
        spaced = spaced.Replace('-', ' ').Replace('_', ' ').Trim();

        if (string.IsNullOrWhiteSpace(spaced))
        {
            return "Endpoint";
        }

        return string.Join(
            " ",
            spaced
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    private static void AddBehavioralFlowNodes(
        CodeGraph graph,
        string startNodeId,
        List<string> nodeIds,
        int maxDepth,
        int maxNodeCount)
    {
        var queue = new Queue<(string NodeId, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startNodeId };
        queue.Enqueue((startNodeId, 0));

        while (queue.Count > 0 && nodeIds.Count < maxNodeCount)
        {
            var current = queue.Dequeue();
            if (current.Depth >= maxDepth)
            {
                continue;
            }

            var nextNodes = graph.Edges
                .Where(edge => edge.SourceId == current.NodeId && edge.Relationship == "Uses")
                .Select(edge => graph.Nodes.FirstOrDefault(node => node.Id == edge.TargetId))
                .Where(node => node is not null && IsFlowNode(node))
                .Select(node => node!)
                .OrderByDescending(GetFlowNodeRank)
                .ThenBy(node => node.Label)
                .Take(3)
                .ToList();

            foreach (var nextNode in nextNodes)
            {
                if (!visited.Add(nextNode.Id))
                {
                    continue;
                }

                AddFlowNode(nodeIds, nextNode.Id);
                queue.Enqueue((nextNode.Id, current.Depth + 1));

                if (nodeIds.Count >= maxNodeCount)
                {
                    break;
                }
            }
        }
    }

    private static bool IsFlowNode(CodeNode? node)
    {
        return node?.Kind is "Controller" or "Service" or "Repository" or "Manager" or "RestClient"
            or "Scanner" or "Detector" or "Analyzer" or "Simplifier";
    }

    private static int GetFlowNodeRank(CodeNode node)
    {
        return node.Kind switch
        {
            "Service" => 100,
            "Analyzer" => 90,
            "Scanner" => 85,
            "Detector" => 85,
            "Simplifier" => 85,
            "Repository" => 80,
            "Manager" => 75,
            "RestClient" => 70,
            _ => 50
        };
    }

    private static void AddFlowNode(List<string> nodeIds, string nodeId)
    {
        if (!nodeIds.Contains(nodeId, StringComparer.OrdinalIgnoreCase))
        {
            nodeIds.Add(nodeId);
        }
    }

    private static Dictionary<string, CodeNode> GetSingleImplementationInterfaceTargets(
        CodeGraph graph,
        List<CodeNode> targetNodes)
    {
        var result = new Dictionary<string, CodeNode>(StringComparer.OrdinalIgnoreCase);
        var interfaceNodes = graph.Nodes
            .Where(node => node.Kind == "Interface")
            .ToList();

        foreach (var interfaceNode in interfaceNodes)
        {
            var interfaceName = GetTypeName(interfaceNode);
            if (!interfaceName.StartsWith("I", StringComparison.Ordinal) || interfaceName.Length < 2)
            {
                continue;
            }

            var implementationName = interfaceName[1..];
            var implementationCandidates = targetNodes
                .Where(node => IsImplementationCandidate(GetTypeName(node), implementationName))
                .ToList();

            if (implementationCandidates.Count == 1)
            {
                result[interfaceName] = implementationCandidates[0];
            }
        }

        return result;
    }

    private static bool HasSingleImplementationInterfaceReference(
        string sourceText,
        CodeNode targetNode,
        Dictionary<string, CodeNode> interfaceTargetsByName)
    {
        return interfaceTargetsByName.Any(pair =>
            pair.Value.Id == targetNode.Id &&
            IsTypeReferenced(sourceText, pair.Key));
    }

    private static bool IsImplementationCandidate(string typeName, string implementationName)
    {
        return typeName.Equals(implementationName, StringComparison.OrdinalIgnoreCase) ||
               typeName.EndsWith(implementationName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBehavioralSourceNode(CodeNode node)
    {
        return node.Kind is "EntryPoint" or "Route" or "Component" or "Controller" or "Service" or "Repository"
            or "Manager" or "RestClient" or "Scanner" or "Detector" or "Analyzer" or "Simplifier";
    }

    private static bool IsBehavioralTargetNode(CodeNode node)
    {
        return node.Kind is "Controller" or "Service" or "Repository" or "Manager" or "RestClient"
            or "Scanner" or "Detector" or "Analyzer" or "Simplifier";
    }

    private static bool IsSourceTextFile(string filePath)
    {
        return filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadSourceText(string rootPath, string sourcePath)
    {
        try
        {
            var rootFullPath = Path.GetFullPath(rootPath);
            var sourceFullPath = Path.GetFullPath(Path.Combine(
                rootFullPath,
                sourcePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = rootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                             Path.DirectorySeparatorChar;

            if (!sourceFullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return File.ReadAllText(sourceFullPath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsTypeReferenced(string sourceText, string typeName)
    {
        var startIndex = 0;
        while (startIndex < sourceText.Length)
        {
            var index = sourceText.IndexOf(typeName, startIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            char? before = index == 0 ? null : sourceText[index - 1];
            var afterIndex = index + typeName.Length;
            char? after = afterIndex >= sourceText.Length ? null : sourceText[afterIndex];

            if (!IsIdentifierCharacter(before) && !IsIdentifierCharacter(after))
            {
                return true;
            }

            startIndex = index + typeName.Length;
        }

        return false;
    }

    private static bool IsIdentifierCharacter(char? character)
    {
        return character is not null &&
               (char.IsLetterOrDigit(character.Value) || character.Value == '_');
    }

    private static void AddEdge(CodeGraph graph, string sourceId, string targetId, string relationship)
    {
        var edgeId = CreateEdgeId(sourceId, targetId, relationship);
        if (graph.Edges.Any(edge => edge.Id == edgeId))
        {
            return;
        }

        graph.Edges.Add(new CodeEdge
        {
            Id = edgeId,
            SourceId = sourceId,
            TargetId = targetId,
            Relationship = relationship
        });
    }

    private static string CreateNodeId(string prefix, string path)
    {
        return $"{prefix}-{ToStableId(path)}";
    }

    private static string CreateEdgeId(string sourceId, string targetId, string relationship)
    {
        return $"{ToStableId(relationship)}-{sourceId}-{targetId}";
    }

    private static string ToStableId(string value)
    {
        return value
            .Replace('\\', '/')
            .Replace("/", "-")
            .Replace(".", "-")
            .Replace(" ", "-")
            .Replace(":", "-");
    }

    private static string NormalizePath(string filePath)
    {
        return filePath.Replace('\\', '/');
    }

    private static string GetKind(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        if (filePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return "ProjectFile";
        }

        if (fileName.Equals("Program.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "EntryPoint";
        }

        if (IsConfigFile(filePath))
        {
            return "Config";
        }

        if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
            HasPathSegment(filePath, "Interfaces"))
        {
            return "Interface";
        }

        if (filePath.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "Controller";
        }

        if (filePath.EndsWith("Repository.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "Repository";
        }

        if (filePath.EndsWith("RestService.cs", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith("ApiClient.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "RestClient";
        }

        if (filePath.EndsWith("Manager.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "Manager";
        }

        if (filePath.EndsWith("Scanner.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "Scanner";
        }

        if (filePath.EndsWith("Detector.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "Detector";
        }

        if (filePath.EndsWith("Analyzer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "Analyzer";
        }

        if (filePath.EndsWith("Simplifier.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "Simplifier";
        }

        if (filePath.EndsWith("Service.cs", StringComparison.OrdinalIgnoreCase))
        {
            return "Service";
        }

        if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) &&
            HasPathSegment(filePath, "Pages"))
        {
            return "Route";
        }

        if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            return "Component";
        }

        if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
            HasPathSegment(filePath, "Entities"))
        {
            return "Entity";
        }

        return "File";
    }

    private static string GetLayer(string filePath)
    {
        if (IsConfigFile(filePath) ||
            filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return "Infrastructure";
        }

        if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
            HasPathSegment(filePath, "Components") ||
            HasPathSegment(filePath, "Pages") ||
            HasPathSegment(filePath, "wwwroot") ||
            HasSegmentEnding(filePath, ".Client"))
        {
            return "Frontend";
        }

        if (HasPathSegment(filePath, "Controllers") ||
            HasSegmentEnding(filePath, ".API"))
        {
            return "Api";
        }

        if (filePath.EndsWith("Repository.cs", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith("DbContext.cs", StringComparison.OrdinalIgnoreCase) ||
            HasPathSegment(filePath, "Migrations") ||
            HasPathSegment(filePath, "Data"))
        {
            return "Data";
        }

        if (HasSegmentEnding(filePath, ".Application") ||
            HasPathSegment(filePath, "Services") ||
            HasPathSegment(filePath, "Interfaces"))
        {
            return "Application";
        }

        if (HasSegmentEnding(filePath, ".Infrastructure") ||
            HasPathSegment(filePath, "Infrastructure"))
        {
            return "Infrastructure";
        }

        return "Application";
    }

    private static string GetLanguage(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "CSharp",
            ".razor" => "Razor",
            ".js" => "JavaScript",
            ".css" => "CSS",
            ".json" => "JSON",
            ".sln" or ".slnx" or ".csproj" => "MSBuild",
            _ => "Unknown"
        };
    }

    private static string GetFramework(string filePath, IReadOnlyList<FrameworkDetectionResult> detections)
    {
        if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            return "Blazor";
        }

        if (HasPathSegment(filePath, "Controllers") ||
            filePath.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase) ||
            HasSegmentEnding(filePath, ".API"))
        {
            return "ASP.NET Core";
        }

        return detections.FirstOrDefault()?.Framework ?? string.Empty;
    }

    private static string CreateDescription(string kind, string filePath)
    {
        return kind switch
        {
            "Route" => $"Razor route-like page discovered by folder convention: {filePath}",
            "Component" => $"Razor component discovered during project scan: {filePath}",
            "Controller" => $"Controller file discovered by naming convention: {filePath}",
            "Service" => $"Service file discovered by naming convention: {filePath}",
            "Repository" => $"Repository file discovered by naming convention: {filePath}",
            "Manager" => $"Client or orchestration manager discovered by naming convention: {filePath}",
            "RestClient" => $"HTTP or REST client wrapper discovered by naming convention: {filePath}",
            "Scanner" => $"Project scanner discovered by naming convention: {filePath}",
            "Detector" => $"Framework detector discovered by naming convention: {filePath}",
            "Analyzer" => $"Codebase analyzer discovered by naming convention: {filePath}",
            "Simplifier" => $"Graph simplifier discovered by naming convention: {filePath}",
            "Config" => $"Configuration file discovered during project scan: {filePath}",
            "ProjectFile" => $"Project or solution file discovered during project scan: {filePath}",
            "EntryPoint" => $"Application entrypoint discovered by file name: {filePath}",
            _ => $"File discovered during project scan: {filePath}"
        };
    }

    private static string GetTypeName(CodeNode node)
    {
        return Path.GetFileNameWithoutExtension(node.Label);
    }

    private static bool IsConfigFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        return fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("launchSettings.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("app.config", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("web.config", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPathSegment(string filePath, string segment)
    {
        return filePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(pathSegment => pathSegment.Equals(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasSegmentEnding(string filePath, string suffix)
    {
        return filePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(pathSegment => pathSegment.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static string RemoveSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    private static bool HasMatchingFeatureName(string fileName, string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return false;
        }

        return Path.GetFileNameWithoutExtension(fileName)
            .StartsWith(featureName, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ActionCandidate(string Name, string ReturnType, string Parameters);

    private sealed record EndpointCandidate(
        string NodeId,
        string HttpMethod,
        string Route,
        string ActionName,
        string RequestType,
        string ResponseType);
}
