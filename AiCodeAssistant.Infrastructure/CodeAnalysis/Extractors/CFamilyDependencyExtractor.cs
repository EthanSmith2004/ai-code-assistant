using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Extractors;

/// <summary>
/// Resolves C/C++ dependencies from quoted <c>#include "..."</c> directives.
/// Angle-bracket includes (<c>#include &lt;...&gt;</c>) are treated as system
/// or library headers and ignored. A quoted include is resolved relative to the
/// including file first, then by matching its path suffix anywhere in the tree.
/// </summary>
public sealed partial class CFamilyDependencyExtractor : DependencyExtractorBase
{
    private static readonly string[] NoExtension = { string.Empty };

    public override string Language => "C/C++";

    protected override IReadOnlyCollection<string> Extensions { get; } = new[]
    {
        ".c", ".h", ".cpp", ".cc", ".cxx", ".hpp", ".hh", ".hxx", ".ipp"
    };

    public override IEnumerable<string> ResolveDependencies(string fromRelativePath, string sourceText, IProjectFileIndex files)
    {
        var fromDirectory = DirectoryOf(fromRelativePath);
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in IncludeRegex().Matches(sourceText))
        {
            var include = match.Groups["path"].Value.Trim();
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            var target = files.ResolveRelative(fromDirectory, include, NoExtension) ?? files.ResolveBySuffix(include);
            if (target is not null && !target.Equals(fromRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                resolved.Add(target);
            }
        }

        return resolved;
    }

    [GeneratedRegex(@"(?m)^\s*#\s*include\s+""(?<path>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex IncludeRegex();
}
