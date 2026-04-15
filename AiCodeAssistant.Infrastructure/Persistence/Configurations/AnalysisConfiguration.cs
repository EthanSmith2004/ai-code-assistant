using AiCodeAssistant.Domain.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiCodeAssistant.Infrastructure.Persistence.Configurations;

public class AnalysisConfiguration : IEntityTypeConfiguration<Analysis>
{
    public void Configure(EntityTypeBuilder<Analysis> builder)
    {
        builder.ToTable("Analyses");

        builder.HasKey(analysis => analysis.Id);

        builder.Property(analysis => analysis.Summary)
            .HasMaxLength(1200)
            .IsRequired();

        builder.Property(analysis => analysis.FileCount)
            .IsRequired();

        builder.Property(analysis => analysis.NodeCount)
            .IsRequired();

        builder.Property(analysis => analysis.EdgeCount)
            .IsRequired();

        builder.Property(analysis => analysis.EndpointCount)
            .IsRequired();

        builder.Property(analysis => analysis.CreatedAt)
            .IsRequired();

        builder.HasIndex(analysis => analysis.ProjectId);
        builder.HasIndex(analysis => analysis.CreatedAt);
    }
}
