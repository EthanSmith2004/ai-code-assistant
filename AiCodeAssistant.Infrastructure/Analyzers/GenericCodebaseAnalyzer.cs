using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Analysis;
using AiCodeAssistant.Domain.Entities;
using AiCodeAssistant.Domain.Graph;
using AiCodeAssistant.Infrastructure.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AiCodeAssistant.Infrastructure.Analyzers;

public class GenericCodebaseAnalyzer : ICodebaseAnalyzer
{
    private readonly IReadOnlyList<ILanguageDependencyExtractor> _dependencyExtractors;
    private readonly IReadOnlyList<IEndpointDetector> _endpointDetectors;

    public GenericCodebaseAnalyzer(
        IEnumerable<ILanguageDependencyExtractor> dependencyExtractors,
        IEnumerable<IEndpointDetector> endpointDetectors)
    {
        _dependencyExtractors = dependencyExtractors.ToList();
        _endpointDetectors = endpointDetectors.ToList();
    }

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
        var fileNodesByPath = new Dictionary<string, CodeNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in scanResult.FilePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = NormalizePath(filePath);
            var parentNode = EnsureFolderNodes(graph, rootNode, normalizedPath, folderNodesByPath, detections);
            var fileNode = CreateFileNode(normalizedPath, detections);

            graph.Nodes.Add(fileNode);
            fileNodesByPath[normalizedPath] = fileNode;
            AddEdge(graph, parentNode.Id, fileNode.Id, "Contains");
            AddProjectRelationship(graph, rootNode, fileNode);
        }

        AddNameBasedRelationships(graph);
        AddSourceTextRelationships(graph, scanResult, cancellationToken);
        AddLanguageDependencyEdges(graph, scanResult, fileNodesByPath, cancellationToken);
        AddAspNetCoreEndpoints(graph, scanResult, cancellationToken);
        AddDetectedEndpoints(graph, scanResult, fileNodesByPath, cancellationToken);
        AddInferredFlows(graph);

        // When a project exposes no endpoints (e.g. a library or a CLI), fall back
        // to import-chain flows so there is still something to visualise.
        if (graph.Endpoints.Count == 0)
        {
            AddDependencyFlows(graph);
        }

        return Task.FromResult(graph);
    }

    /// <summary>
    /// Adds language-agnostic "Imports" edges by running each registered
    /// dependency extractor over the files it understands and resolving the
    /// imports it finds to other files in the project. This is what makes the
    /// graph meaningful on non-.NET codebases (JS/TS, Python, Go, Java, C/C++).
    /// </summary>
    private void AddLanguageDependencyEdges(
        CodeGraph graph,
        ProjectScanResult scanResult,
        Dictionary<string, CodeNode> fileNodesByPath,
        CancellationToken cancellationToken)
    {
        if (_dependencyExtractors.Count == 0)
        {
            return;
        }

        var index = new ProjectFileIndex(fileNodesByPath.Keys);

        foreach (var (path, sourceNode) in fileNodesByPath)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractor = _dependencyExtractors.FirstOrDefault(candidate => candidate.CanHandle(path));
            if (extractor is null)
            {
                continue;
            }

            var sourceText = TryReadSourceText(scanResult.RootPath, path);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                continue;
            }

            foreach (var targetPath in extractor.ResolveDependencies(path, sourceText, index))
            {
                if (fileNodesByPath.TryGetValue(NormalizePath(targetPath), out var targetNode) &&
                    targetNode.Id != sourceNode.Id)
                {
                    AddEdge(graph, sourceNode.Id, targetNode.Id, "Imports");
                }
            }
        }
    }

    /// <summary>
    /// Runs the registered endpoint detectors over each source file and adds an
    /// endpoint node (linked to its defining file via "MapsTo") for every route
    /// found. This brings Express, FastAPI/Flask, Spring, Gin, Rails, ... endpoints
    /// into the graph alongside the dedicated ASP.NET Core detection.
    /// </summary>
    private void AddDetectedEndpoints(
        CodeGraph graph,
        ProjectScanResult scanResult,
        Dictionary<string, CodeNode> fileNodesByPath,
        CancellationToken cancellationToken)
    {
        if (_endpointDetectors.Count == 0)
        {
            return;
        }

        foreach (var (path, fileNode) in fileNodesByPath)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detector = _endpointDetectors.FirstOrDefault(candidate => candidate.CanHandle(path));
            if (detector is null)
            {
                continue;
            }

            var sourceText = TryReadSourceText(scanResult.RootPath, path);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                continue;
            }

            foreach (var detected in detector.Detect(sourceText))
            {
                var route = NormalizeEndpointRoute(detected.Route);
                if (string.IsNullOrWhiteSpace(route))
                {
                    continue;
                }

                var method = detected.HttpMethod.ToUpperInvariant();
                var endpointId = CreateNodeId("endpoint", $"{path}-{method}-{route}");

                if (graph.Nodes.All(node => node.Id != endpointId))
                {
                    graph.Nodes.Add(new CodeNode
                    {
                        Id = endpointId,
                        Label = $"{method} {route}",
                        Kind = "Endpoint",
                        Layer = "Api",
                        Language = fileNode.Language,
                        Framework = fileNode.Framework,
                        SourcePath = path,
                        Description = $"HTTP endpoint defined in {fileNode.Label}."
                    });
                }

                if (graph.Endpoints.All(endpoint => endpoint.NodeId != endpointId))
                {
                    graph.Endpoints.Add(new EndpointInfo
                    {
                        NodeId = endpointId,
                        HttpMethod = method,
                        Route = route,
                        RequestType = "None",
                        ResponseType = "Unknown"
                    });
                }

                AddEdge(graph, endpointId, fileNode.Id, "MapsTo");
            }
        }
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
            var targetId = graph.Edges
                .FirstOrDefault(edge => edge.SourceId == endpoint.NodeId && edge.Relationship == "MapsTo")
                ?.TargetId;

            if (string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            AddFlowNode(nodeIds, targetId);

            // .NET controllers fan out through "Uses" edges; other stacks fan out
            // through "Imports" edges from the handler file.
            var targetHasImports = graph.Edges.Any(edge =>
                edge.Relationship == "Imports" &&
                edge.SourceId.Equals(targetId, StringComparison.OrdinalIgnoreCase));

            AddBehavioralFlowNodes(graph, targetId, nodeIds, maxDepth: 3, maxNodeCount: 8);
            if (targetHasImports)
            {
                AddImportFlowNodes(graph, targetId, nodeIds, maxDepth: 4, maxNodeCount: 10);
            }

            var minimumNodes = targetHasImports ? 2 : 3;
            if (nodeIds.Count < minimumNodes)
            {
                continue;
            }

            graph.Flows.Add(new CodeFlow
            {
                Id = CreateNodeId("flow", endpoint.NodeId),
                // Name by method + route so every endpoint flow is unique and clear
                // (a controller with several routes no longer yields duplicate names).
                Name = $"{endpoint.HttpMethod} {endpoint.Route}",
                Description = $"Request path for {endpoint.HttpMethod} {endpoint.Route}: how the endpoint reaches the code that handles it.",
                NodeIds = nodeIds
            });
        }
    }

    /// <summary>
    /// Builds "flows" by following intra-project import edges from each top-level
    /// module. This gives non-.NET codebases (JS/TS, Python, Go, Java, C/C++)
    /// something meaningful to visualise, since they have no ASP.NET endpoints.
    /// .NET graphs have no "Imports" edges, so this is a no-op for them.
    /// </summary>
    private static void AddDependencyFlows(CodeGraph graph)
    {
        var importEdges = graph.Edges
            .Where(edge => edge.Relationship == "Imports")
            .ToList();
        if (importEdges.Count == 0)
        {
            return;
        }

        var nodesById = graph.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var importedTargets = importEdges
            .Select(edge => edge.TargetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var outDegree = importEdges
            .GroupBy(edge => edge.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        // Roots are modules that import others but are imported by no one (entry
        // points / top-level files). For purely cyclic graphs, fall back to the
        // most import-heavy modules so there is always something to show.
        var roots = outDegree.Keys
            .Where(id => !importedTargets.Contains(id))
            .ToList();

        if (roots.Count == 0)
        {
            roots = outDegree
                .OrderByDescending(pair => pair.Value)
                .Take(8)
                .Select(pair => pair.Key)
                .ToList();
        }

        foreach (var rootId in roots.OrderByDescending(id => outDegree.GetValueOrDefault(id)).Take(14))
        {
            if (!nodesById.TryGetValue(rootId, out var rootNode))
            {
                continue;
            }

            var nodeIds = new List<string> { rootId };
            AddImportFlowNodes(graph, rootId, nodeIds, maxDepth: 4, maxNodeCount: 12);

            if (nodeIds.Count < 2)
            {
                continue;
            }

            graph.Flows.Add(new CodeFlow
            {
                Id = CreateNodeId("depflow", rootId),
                Name = $"{rootNode.Label} dependencies",
                Description = $"Modules reached from {rootNode.Label} by following its imports.",
                NodeIds = nodeIds
            });
        }
    }

    private static void AddImportFlowNodes(
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

            var nextIds = graph.Edges
                .Where(edge => edge.Relationship == "Imports" &&
                               edge.SourceId.Equals(current.NodeId, StringComparison.OrdinalIgnoreCase))
                .Select(edge => edge.TargetId)
                .ToList();

            foreach (var nextId in nextIds)
            {
                if (!visited.Add(nextId))
                {
                    continue;
                }

                AddFlowNode(nodeIds, nextId);
                queue.Enqueue((nextId, current.Depth + 1));

                if (nodeIds.Count >= maxNodeCount)
                {
                    break;
                }
            }
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

            if (string.IsNullOrWhiteSpace(target))
            {
                return "Explain Flow";
            }

            var humanizedTarget = HumanizeRouteSegment(target);

            if (humanizedTarget.Equals("Flow", StringComparison.OrdinalIgnoreCase))
            {
                return "Explain Flow";
            }

            return humanizedTarget.EndsWith(" Flow", StringComparison.OrdinalIgnoreCase)
                ? $"Explain {humanizedTarget}"
                : $"Explain {humanizedTarget} Flow";
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

        // Cross-language heuristics so non-.NET source files are classified too.
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

        if (IsTestFile(filePath))
        {
            return "Test";
        }

        if (IsGenericEntryPoint(fileName))
        {
            return "EntryPoint";
        }

        if (extension is ".jsx" or ".tsx" or ".vue" or ".svelte" ||
            EndsWithName(nameWithoutExtension, "component"))
        {
            return "Component";
        }

        if (HasPathSegment(filePath, "controllers") || EndsWithName(nameWithoutExtension, "controller"))
        {
            return "Controller";
        }

        if (HasPathSegment(filePath, "pages") || HasPathSegment(filePath, "views") || HasPathSegment(filePath, "routes"))
        {
            return "Route";
        }

        if (EndsWithName(nameWithoutExtension, "service") || HasPathSegment(filePath, "services"))
        {
            return "Service";
        }

        if (EndsWithName(nameWithoutExtension, "repository") || HasPathSegment(filePath, "repositories"))
        {
            return "Repository";
        }

        if (HasPathSegment(filePath, "models") || HasPathSegment(filePath, "entities") ||
            HasPathSegment(filePath, "domain") || EndsWithName(nameWithoutExtension, "model"))
        {
            return "Entity";
        }

        if (extension is ".css" or ".scss" or ".sass" or ".less")
        {
            return "Style";
        }

        if (IsSourceCodeExtension(extension))
        {
            return "Module";
        }

        return "File";
    }

    private static bool EndsWithName(string nameWithoutExtension, string suffix)
    {
        return nameWithoutExtension.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGenericEntryPoint(string fileName)
    {
        return fileName.ToLowerInvariant() is
            "main.py" or "app.py" or "manage.py" or "wsgi.py" or "asgi.py" or
            "main.go" or "main.rs" or "main.java" or "main.kt" or
            "server.js" or "server.ts" or "server.mjs" or "app.js" or "app.ts" or "index.php";
    }

    private static bool IsTestFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        return HasPathSegment(filePath, "tests") ||
               HasPathSegment(filePath, "__tests__") ||
               fileName.Contains(".test.") ||
               fileName.Contains(".spec.") ||
               fileName.StartsWith("test_", StringComparison.Ordinal) ||
               fileName.EndsWith("_test.go", StringComparison.Ordinal) ||
               fileName.EndsWith("_test.py", StringComparison.Ordinal);
    }

    private static bool IsSourceCodeExtension(string extension)
    {
        return extension is ".js" or ".jsx" or ".ts" or ".tsx" or ".mjs" or ".cjs" or ".mts" or ".cts"
            or ".py" or ".pyi" or ".go" or ".java" or ".kt" or ".kts" or ".rb" or ".php" or ".rs"
            or ".swift" or ".scala" or ".dart" or ".c" or ".h" or ".cpp" or ".cc" or ".cxx"
            or ".hpp" or ".hh" or ".hxx" or ".vue" or ".svelte";
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

        // Generic, cross-language layer inference by extension and folder convention.
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension is ".jsx" or ".tsx" or ".vue" or ".svelte" or ".css" or ".scss" or ".sass" or ".less" or ".html" or ".htm" ||
            HasPathSegment(filePath, "components") || HasPathSegment(filePath, "client") ||
            HasPathSegment(filePath, "frontend") || HasPathSegment(filePath, "public") ||
            HasPathSegment(filePath, "ui") || HasPathSegment(filePath, "views") || HasPathSegment(filePath, "pages"))
        {
            return "Frontend";
        }

        if (HasPathSegment(filePath, "server") || HasPathSegment(filePath, "backend") ||
            HasPathSegment(filePath, "api") || HasPathSegment(filePath, "routes") || HasPathSegment(filePath, "handlers"))
        {
            return "Api";
        }

        if (HasPathSegment(filePath, "models") || HasPathSegment(filePath, "entities") ||
            HasPathSegment(filePath, "db") || HasPathSegment(filePath, "database") ||
            HasPathSegment(filePath, "migrations") || HasPathSegment(filePath, "repositories") ||
            HasPathSegment(filePath, "prisma"))
        {
            return "Data";
        }

        return "Application";
    }

    private static string GetLanguage(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "CSharp",
            ".razor" => "Razor",
            ".js" or ".jsx" or ".mjs" or ".cjs" => "JavaScript",
            ".ts" or ".tsx" or ".mts" or ".cts" => "TypeScript",
            ".py" or ".pyi" => "Python",
            ".go" => "Go",
            ".java" => "Java",
            ".kt" or ".kts" => "Kotlin",
            ".rb" => "Ruby",
            ".php" => "PHP",
            ".rs" => "Rust",
            ".swift" => "Swift",
            ".c" or ".h" => "C",
            ".cpp" or ".cc" or ".cxx" or ".hpp" or ".hh" or ".hxx" or ".ipp" => "C++",
            ".vue" => "Vue",
            ".svelte" => "Svelte",
            ".css" or ".scss" or ".sass" or ".less" => "CSS",
            ".html" or ".htm" => "HTML",
            ".json" => "JSON",
            ".yml" or ".yaml" => "YAML",
            ".xml" => "XML",
            ".sql" => "SQL",
            ".sh" or ".bash" => "Shell",
            ".md" or ".mdx" => "Markdown",
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

    private static readonly HashSet<string> ConfigFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // .NET
        "launchSettings.json", "app.config", "web.config", "global.json", "nuget.config",
        // Node / JS / TS
        "package.json", "package-lock.json", "tsconfig.json", "jsconfig.json", "yarn.lock",
        "pnpm-lock.yaml", ".babelrc", ".eslintrc", ".eslintrc.json", ".prettierrc",
        // Python
        "requirements.txt", "pyproject.toml", "setup.py", "setup.cfg", "pipfile",
        "pipfile.lock", "poetry.lock", "tox.ini", "environment.yml",
        // Go
        "go.mod", "go.sum",
        // Rust
        "cargo.toml", "cargo.lock",
        // Java / Kotlin / JVM
        "pom.xml", "build.gradle", "build.gradle.kts", "settings.gradle", "settings.gradle.kts",
        // Ruby / PHP
        "gemfile", "gemfile.lock", "composer.json", "composer.lock",
        // Build / container / CI
        "dockerfile", "docker-compose.yml", "docker-compose.yaml", "makefile", "cmakelists.txt",
        ".gitignore", ".dockerignore", ".editorconfig", "procfile"
    };

    private static bool IsConfigFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        if (ConfigFileNames.Contains(fileName) ||
            fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(".env", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Bundler / framework config files: vite.config.ts, webpack.config.js, next.config.mjs, etc.
        var lower = fileName.ToLowerInvariant();
        return lower.Contains(".config.") &&
               (lower.EndsWith(".js") || lower.EndsWith(".ts") || lower.EndsWith(".mjs") ||
                lower.EndsWith(".cjs") || lower.EndsWith(".json"));
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
