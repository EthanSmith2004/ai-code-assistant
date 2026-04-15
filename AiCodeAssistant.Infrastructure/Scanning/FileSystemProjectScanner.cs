using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Domain.Analysis;

namespace AiCodeAssistant.Infrastructure.Scanning;

public class FileSystemProjectScanner : IProjectScanner
{
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "obj",
        "node_modules"
    };

    public Task<ProjectScanResult> ScanAsync(ProjectScanRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RootPath))
        {
            throw new ArgumentException("A root path is required.", nameof(request));
        }

        var rootPath = Path.GetFullPath(request.RootPath);

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Project path was not found: {rootPath}");
        }

        var filePaths = new List<string>();
        ScanDirectory(rootPath, rootPath, filePaths, cancellationToken);

        return Task.FromResult(new ProjectScanResult
        {
            RootPath = rootPath,
            FilePaths = filePaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList()
        });
    }

    private static void ScanDirectory(
        string rootPath,
        string currentPath,
        List<string> filePaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var filePath in Directory.EnumerateFiles(currentPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            filePaths.Add(Path.GetRelativePath(rootPath, filePath));
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(currentPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directoryName = Path.GetFileName(directoryPath);
            if (IgnoredDirectoryNames.Contains(directoryName))
            {
                continue;
            }

            ScanDirectory(rootPath, directoryPath, filePaths, cancellationToken);
        }
    }
}
