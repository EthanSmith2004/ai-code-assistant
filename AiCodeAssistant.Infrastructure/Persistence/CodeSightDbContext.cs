using AiCodeAssistant.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiCodeAssistant.Infrastructure.Persistence;

public class CodeSightDbContext : DbContext
{
    public CodeSightDbContext(DbContextOptions<CodeSightDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Analysis> Analyses => Set<Analysis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CodeSightDbContext).Assembly);
    }
}
