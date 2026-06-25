using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Extractors;

/// <summary>
/// Shared plumbing for language dependency extractors: extension matching and
/// project-relative path math used to turn import specifiers into file paths.
/// </summary>
public abstract class DependencyExtractorBase : ILanguageDependencyExtractor
{
    public abstract string Language { get; }

    protected abstract IReadOnlyCollection<string> Extensions { get; }

    public bool CanHandle(string relativePath)
    {
        return Extensions.Any(extension => relativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    public abstract IEnumerable<string> ResolveDependencies(string fromRelativePath, string sourceText, IProjectFileIndex files);

    protected static string DirectoryOf(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : normalized[..lastSlash];
    }

    /// <summary>Combines a directory with a relative path, collapsing . and .. segments.</summary>
    protected static string Combine(string directory, string relative)
    {
        var combined = $"{directory}/{relative}".Replace('\\', '/');
        var stack = new List<string>();

        foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
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

    protected static bool IsRelativeSpecifier(string specifier)
    {
        return specifier.StartsWith('.') || specifier.StartsWith('/');
    }
}
