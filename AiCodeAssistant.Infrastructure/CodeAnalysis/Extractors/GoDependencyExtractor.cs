using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Extractors;

/// <summary>
/// Resolves Go package imports to the .go files that make up the imported
/// package. Without parsing go.mod this is heuristic: it matches the longest
/// trailing segment(s) of an import path against a package directory in the
/// project, which captures intra-module imports while ignoring external ones.
/// </summary>
public sealed partial class GoDependencyExtractor : DependencyExtractorBase
{
    public override string Language => "Go";

    protected override IReadOnlyCollection<string> Extensions { get; } = new[] { ".go" };

    public override IEnumerable<string> ResolveDependencies(string fromRelativePath, string sourceText, IProjectFileIndex files)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var goFiles = files.AllFiles
            .Where(file => file.EndsWith(".go", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var importPath in ExtractImportPaths(sourceText))
        {
            ResolvePackage(importPath, fromRelativePath, goFiles, resolved);
        }

        return resolved;
    }

    private static IEnumerable<string> ExtractImportPaths(string sourceText)
    {
        // Single-line: import "path"  /  import alias "path"
        foreach (Match match in SingleImportRegex().Matches(sourceText))
        {
            yield return match.Groups["path"].Value;
        }

        // Block: import ( "a" \n alias "b" \n )
        foreach (Match block in ImportBlockRegex().Matches(sourceText))
        {
            foreach (Match path in QuotedPathRegex().Matches(block.Groups["body"].Value))
            {
                yield return path.Groups["path"].Value;
            }
        }
    }

    private static void ResolvePackage(string importPath, string fromRelativePath, List<string> goFiles, HashSet<string> resolved)
    {
        var segments = importPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return;
        }

        for (var take = segments.Length; take >= 1; take--)
        {
            var suffix = string.Join('/', segments[^take..]);
            var matches = goFiles
                .Where(file => DirectoryEndsWith(DirectoryOf(file), suffix))
                .Where(file => !file.Equals(fromRelativePath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Single-segment matches are accepted only when unambiguous to avoid
            // linking unrelated packages that merely share a final name.
            if (matches.Count == 0 || (take == 1 && DistinctDirectories(matches) > 1))
            {
                continue;
            }

            foreach (var file in matches)
            {
                resolved.Add(file);
            }

            return;
        }
    }

    private static bool DirectoryEndsWith(string directory, string suffix)
    {
        return directory.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
               directory.EndsWith("/" + suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static int DistinctDirectories(IEnumerable<string> files)
    {
        return files.Select(DirectoryOf).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    [GeneratedRegex(@"(?m)^\s*import\s+(?:[\w.]+\s+)?""(?<path>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex SingleImportRegex();

    [GeneratedRegex(@"import\s*\((?<body>[\s\S]*?)\)", RegexOptions.CultureInvariant)]
    private static partial Regex ImportBlockRegex();

    [GeneratedRegex(@"(?:[\w.]+\s+)?""(?<path>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedPathRegex();
}
