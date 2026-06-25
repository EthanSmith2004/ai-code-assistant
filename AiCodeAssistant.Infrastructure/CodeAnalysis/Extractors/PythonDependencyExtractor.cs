using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Extractors;

/// <summary>
/// Resolves Python dependencies from <c>import a.b.c</c> and
/// <c>from a.b import c</c> statements, including relative imports
/// (<c>from . import x</c>, <c>from ..pkg import y</c>). Standard-library and
/// third-party modules that do not map to a project file are ignored.
/// </summary>
public sealed partial class PythonDependencyExtractor : DependencyExtractorBase
{
    public override string Language => "Python";

    protected override IReadOnlyCollection<string> Extensions { get; } = new[] { ".py", ".pyi" };

    public override IEnumerable<string> ResolveDependencies(string fromRelativePath, string sourceText, IProjectFileIndex files)
    {
        var fromDirectory = DirectoryOf(fromRelativePath);
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ImportRegex().Matches(sourceText))
        {
            foreach (var entry in match.Groups["modules"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var module = entry.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                AddIfFound(ResolveAbsoluteModule(module, files), resolved);
            }
        }

        foreach (Match match in FromImportRegex().Matches(sourceText))
        {
            var dots = match.Groups["dots"].Value.Length;
            var module = match.Groups["module"].Value;
            var names = match.Groups["names"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(name => !string.IsNullOrWhiteSpace(name) && name != "*")
                .Select(name => name!)
                .ToList();

            if (dots == 0)
            {
                AddIfFound(ResolveAbsoluteModule(module, files), resolved);
                foreach (var name in names)
                {
                    AddIfFound(ResolveAbsoluteModule($"{module}.{name}", files), resolved);
                }
            }
            else
            {
                ResolveRelativeImport(fromDirectory, dots, module, names, files, resolved);
            }
        }

        return resolved;
    }

    private static string? ResolveAbsoluteModule(string? dottedModule, IProjectFileIndex files)
    {
        if (string.IsNullOrWhiteSpace(dottedModule))
        {
            return null;
        }

        var path = dottedModule.Trim().Replace('.', '/');
        return files.ResolveBySuffix($"{path}.py") ?? files.ResolveBySuffix($"{path}/__init__.py");
    }

    private static void ResolveRelativeImport(
        string fromDirectory,
        int dots,
        string module,
        IReadOnlyList<string> names,
        IProjectFileIndex files,
        HashSet<string> resolved)
    {
        var ups = string.Concat(Enumerable.Repeat("../", Math.Max(0, dots - 1)));
        var moduleSubPath = string.IsNullOrEmpty(module) ? string.Empty : module.Replace('.', '/');
        var basePath = Combine(fromDirectory, ups + moduleSubPath);

        AddIfExists(files, $"{basePath}.py", resolved);
        AddIfExists(files, $"{basePath}/__init__.py", resolved);

        foreach (var name in names)
        {
            var namePath = Combine(basePath, name);
            AddIfExists(files, $"{namePath}.py", resolved);
            AddIfExists(files, $"{namePath}/__init__.py", resolved);
        }
    }

    private static void AddIfExists(IProjectFileIndex files, string candidate, HashSet<string> resolved)
    {
        if (files.Contains(candidate))
        {
            resolved.Add(candidate);
        }
    }

    private static void AddIfFound(string? resolvedPath, HashSet<string> resolved)
    {
        if (resolvedPath is not null)
        {
            resolved.Add(resolvedPath);
        }
    }

    [GeneratedRegex(@"(?m)^[ \t]*import[ \t]+(?<modules>[^\n#]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ImportRegex();

    [GeneratedRegex(@"(?m)^[ \t]*from[ \t]+(?<dots>\.*)(?<module>[\w.]*)[ \t]+import[ \t]+(?<names>[^\n#]+)", RegexOptions.CultureInvariant)]
    private static partial Regex FromImportRegex();
}
