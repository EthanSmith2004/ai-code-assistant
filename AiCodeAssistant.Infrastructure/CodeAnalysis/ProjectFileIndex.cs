using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis;

/// <summary>
/// In-memory index of a scanned project's files, used by dependency extractors
/// to resolve import specifiers to real files. Built once per analysis.
/// </summary>
public sealed class ProjectFileIndex : IProjectFileIndex
{
    private readonly HashSet<string> _files;
    private readonly List<string> _orderedFiles;

    public ProjectFileIndex(IEnumerable<string> relativePaths)
    {
        _orderedFiles = relativePaths
            .Select(Normalize)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _files = new HashSet<string>(_orderedFiles, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> AllFiles => _orderedFiles;

    public bool Contains(string relativePath)
    {
        return _files.Contains(Normalize(relativePath));
    }

    public string? ResolveRelative(string fromDirectory, string specifier, IReadOnlyList<string> candidateExtensions)
    {
        if (string.IsNullOrWhiteSpace(specifier))
        {
            return null;
        }

        var combined = CollapseSegments($"{Normalize(fromDirectory)}/{Normalize(specifier)}");
        if (string.IsNullOrEmpty(combined))
        {
            return null;
        }

        // Specifier that already names a real file (with or without extension).
        if (_files.Contains(combined))
        {
            return GetActual(combined);
        }

        foreach (var extension in candidateExtensions)
        {
            var withExtension = combined + extension;
            if (_files.Contains(withExtension))
            {
                return GetActual(withExtension);
            }
        }

        foreach (var extension in candidateExtensions)
        {
            var indexFile = $"{combined}/index{extension}";
            if (_files.Contains(indexFile))
            {
                return GetActual(indexFile);
            }
        }

        return null;
    }

    public string? ResolveBySuffix(string pathSuffix)
    {
        var suffix = Normalize(pathSuffix).TrimStart('/');
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return null;
        }

        string? match = null;
        foreach (var file in _orderedFiles)
        {
            if (file.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith("/" + suffix, StringComparison.OrdinalIgnoreCase))
            {
                if (match is not null)
                {
                    return null; // ambiguous - avoid inventing a wrong edge
                }

                match = file;
            }
        }

        return match;
    }

    private string GetActual(string normalized)
    {
        // Return the stored casing/spelling rather than the lookup key.
        return _orderedFiles.FirstOrDefault(file => file.Equals(normalized, StringComparison.OrdinalIgnoreCase))
               ?? normalized;
    }

    private static string Normalize(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
    }

    private static string CollapseSegments(string path)
    {
        var stack = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (segment)
            {
                case ".":
                    continue;
                case "..":
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    continue;
                default:
                    stack.Add(segment);
                    break;
            }
        }

        return string.Join('/', stack);
    }
}
