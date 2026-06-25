using System.Text.RegularExpressions;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Infrastructure.CodeAnalysis.Extractors;

/// <summary>
/// Resolves Java/Kotlin <c>import</c> statements to project files by mapping a
/// fully-qualified name to a package path (com.example.Foo -> com/example/Foo).
/// Wildcard imports (<c>com.example.*</c>) link to every file in that package.
/// Imports that resolve outside the project (JDK, libraries) are ignored.
/// </summary>
public sealed partial class JavaDependencyExtractor : DependencyExtractorBase
{
    public override string Language => "Java/Kotlin";

    protected override IReadOnlyCollection<string> Extensions { get; } = new[] { ".java", ".kt", ".kts" };

    public override IEnumerable<string> ResolveDependencies(string fromRelativePath, string sourceText, IProjectFileIndex files)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceFiles = files.AllFiles
            .Where(file => file.EndsWith(".java", StringComparison.OrdinalIgnoreCase) ||
                           file.EndsWith(".kt", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (Match match in ImportRegex().Matches(sourceText))
        {
            var fqn = match.Groups["fqn"].Value.Trim();

            if (fqn.EndsWith(".*", StringComparison.Ordinal))
            {
                var packagePath = fqn[..^2].Replace('.', '/');
                foreach (var file in sourceFiles.Where(file => DirectoryEndsWith(DirectoryOf(file), packagePath)))
                {
                    resolved.Add(file);
                }

                continue;
            }

            var path = fqn.Replace('.', '/');
            var target = files.ResolveBySuffix($"{path}.java") ?? files.ResolveBySuffix($"{path}.kt");
            if (target is not null)
            {
                resolved.Add(target);
            }
        }

        return resolved;
    }

    private static bool DirectoryEndsWith(string directory, string suffix)
    {
        return directory.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
               directory.EndsWith("/" + suffix, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?m)^\s*import\s+(?:static\s+)?(?<fqn>[\w.]+(?:\.\*)?)\s*;?", RegexOptions.CultureInvariant)]
    private static partial Regex ImportRegex();
}
