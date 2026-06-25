using AiCodeAssistant.Domain.Contracts.Projects;

namespace AiCodeAssistant.API.Samples;

/// <summary>
/// The set of bundled demo codebases (one per framework family) that users can
/// scan from the workspace. Folders live under the repo's <c>samples/</c>
/// directory; only demos whose folder is present on disk are returned.
/// </summary>
public class SampleCatalog
{
    private static readonly SampleDescriptor[] Descriptors =
    {
        new("aspnet-store", "Store API", "ASP.NET Core", "C#",
            "Products REST API with a controller, service, repository, and model."),
        new("express-shop", "Shopping Cart", "Express", "TypeScript",
            "Express cart API with routes, a controller, a service, and models."),
        new("fastapi-tasks", "Task Manager", "FastAPI", "Python",
            "FastAPI task service with route handlers, a service, and a model."),
        new("gin-books", "Book Catalog", "Gin", "Go",
            "Gin HTTP API with route handlers and a store package."),
        new("spring-orders", "Order Service", "Spring Boot", "Java",
            "Spring Boot order API with a controller, service, repository, and model.")
    };

    private readonly string? _root;

    public SampleCatalog(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _root = ResolveRoot(environment, configuration);
    }

    public IReadOnlyList<SampleProjectDto> GetAll()
    {
        if (_root is null)
        {
            return Array.Empty<SampleProjectDto>();
        }

        return Descriptors
            .Select(descriptor => new { descriptor, path = Path.Combine(_root, descriptor.Id) })
            .Where(item => Directory.Exists(item.path))
            .Select(item => new SampleProjectDto
            {
                Id = item.descriptor.Id,
                Name = item.descriptor.Name,
                Framework = item.descriptor.Framework,
                Language = item.descriptor.Language,
                Description = item.descriptor.Description,
                Path = item.path
            })
            .ToList();
    }

    private static string? ResolveRoot(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var candidates = new List<string>();

        var configured = configuration["Samples:Path"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(configured);
        }

        var content = environment.ContentRootPath;
        candidates.Add(Path.Combine(content, "samples"));
        candidates.Add(Path.Combine(content, "..", "samples"));
        candidates.Add(Path.Combine(content, "..", "..", "samples"));

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
            {
                return full;
            }
        }

        return null;
    }

    private sealed record SampleDescriptor(string Id, string Name, string Framework, string Language, string Description);
}
