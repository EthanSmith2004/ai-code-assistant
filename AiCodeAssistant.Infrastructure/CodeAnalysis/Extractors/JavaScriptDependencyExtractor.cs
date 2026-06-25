using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Extractors;

/// <summary>
/// Resolves ES module / CommonJS dependencies for JavaScript, TypeScript, JSX,
/// TSX, and single-file components (Vue/Svelte). Only relative imports are
/// followed; bare package imports (e.g. "react") are treated as external.
/// </summary>
public sealed partial class JavaScriptDependencyExtractor : DependencyExtractorBase
{
    private static readonly string[] ResolutionExtensions =
    {
        "", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", ".mts", ".cts", ".json", ".vue", ".svelte"
    };

    public override string Language => "JavaScript/TypeScript";

    protected override IReadOnlyCollection<string> Extensions { get; } = new[]
    {
        ".js", ".jsx", ".ts", ".tsx", ".mjs", ".cjs", ".mts", ".cts", ".vue", ".svelte"
    };

    public override IEnumerable<string> ResolveDependencies(string fromRelativePath, string sourceText, IProjectFileIndex files)
    {
        var fromDirectory = DirectoryOf(fromRelativePath);
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in FromImportRegex().Matches(sourceText))
        {
            TryResolve(match.Groups["spec"].Value, fromDirectory, files, resolved);
        }

        foreach (Match match in SideEffectImportRegex().Matches(sourceText))
        {
            TryResolve(match.Groups["spec"].Value, fromDirectory, files, resolved);
        }

        return resolved;
    }

    private static void TryResolve(string specifier, string fromDirectory, IProjectFileIndex files, HashSet<string> resolved)
    {
        if (string.IsNullOrWhiteSpace(specifier) || !IsRelativeSpecifier(specifier))
        {
            return;
        }

        var target = files.ResolveRelative(fromDirectory, specifier, ResolutionExtensions);
        if (target is not null)
        {
            resolved.Add(target);
        }
    }

    // import x from '...', export { y } from '...', require('...'), import('...')
    [GeneratedRegex(@"(?:\bfrom|\brequire\s*\(|\bimport\s*\()\s*['""](?<spec>[^'""]+)['""]", RegexOptions.CultureInvariant)]
    private static partial Regex FromImportRegex();

    // side-effect import: import '...';
    [GeneratedRegex(@"\bimport\s+['""](?<spec>[^'""]+)['""]", RegexOptions.CultureInvariant)]
    private static partial Regex SideEffectImportRegex();
}
