using AiCodeAssistant.Domain.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiCodeAssistant.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(600)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.HasMany(user => user.Projects)
            .WithOne(project => project.User)
            .HasForeignKey(project => project.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
