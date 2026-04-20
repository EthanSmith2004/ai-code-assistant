using AiCodeAssistant.Domain.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiCodeAssistant.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(project => project.FrameworkType)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(project => project.SourceIdentifier)
            .HasMaxLength(600)
            .IsRequired();

        builder.Property(project => project.CreatedAt)
            .IsRequired();

        builder.Property(project => project.UpdatedAt)
            .IsRequired();

        builder.HasIndex(project => project.UserId);

        builder.HasIndex(project => new { project.UserId, project.SourceIdentifier })
            .IsUnique();

        builder.HasMany(project => project.Analyses)
            .WithOne(analysis => analysis.Project)
            .HasForeignKey(analysis => analysis.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
