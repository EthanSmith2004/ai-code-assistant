namespace AiCodeAssistant.Application.Interfaces;

/// <summary>
/// A lookup over every file discovered in a scanned project, with helpers that
/// language dependency extractors use to resolve import specifiers to real
/// files. All paths are project-relative and use forward slashes.
/// </summary>
public interface IProjectFileIndex
{
    IReadOnlyCollection<string> AllFiles { get; }

    /// <summary>True when an exact relative path exists in the project.</summary>
    bool Contains(string relativePath);

    /// <summary>
    /// Resolves a relative import specifier (e.g. <c>./util</c>, <c>../db/client</c>)
    /// from the given directory, trying each candidate extension and an
    /// <c>index.&lt;ext&gt;</c> file inside a matched folder. Returns the resolved
    /// relative path, or null when nothing matches inside the project.
    /// </summary>
    string? ResolveRelative(string fromDirectory, string specifier, IReadOnlyList<string> candidateExtensions);

    /// <summary>
    /// Finds the single best file whose path ends with the given suffix
    /// (e.g. a package path like <c>service/UserService.java</c>). Used to resolve
    /// non-relative imports such as Go/Java/C package or include paths. Returns
    /// null when there is no match or the match is ambiguous.
    /// </summary>
    string? ResolveBySuffix(string pathSuffix);
}
