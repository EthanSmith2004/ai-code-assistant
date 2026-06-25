namespace AiCodeAssistant.Application.Interfaces;

/// <summary>
/// Extracts intra-project dependencies (imports / requires / includes) for one
/// language family. Implementations are language-specific but provider-agnostic:
/// given a file's source text they return the relative paths of other files in
/// the same project that the file depends on. External/library imports are
/// intentionally ignored so the resulting graph stays about the project itself.
/// </summary>
public interface ILanguageDependencyExtractor
{
    /// <summary>Language label used on edges/diagnostics (e.g. "TypeScript").</summary>
    string Language { get; }

    /// <summary>True when this extractor understands the given file extension.</summary>
    bool CanHandle(string relativePath);

    /// <summary>
    /// Returns project-relative paths (forward slashes) of files the given file
    /// depends on. Targets that cannot be resolved inside the project are omitted.
    /// </summary>
    IEnumerable<string> ResolveDependencies(string fromRelativePath, string sourceText, IProjectFileIndex files);
}
