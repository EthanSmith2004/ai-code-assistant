using System;
using AiCodeAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace AiCodeAssistant.Infrastructure.Migrations
{
    [DbContext(typeof(CodeSightDbContext))]
    partial class CodeSightDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("AiCodeAssistant.Domain.Persistence.Analysis", builder =>
            {
                builder.Property<Guid>("Id")
                    .HasColumnType("char(36)");

                builder.Property<DateTime>("CreatedAt")
                    .HasColumnType("datetime(6)");

                builder.Property<int>("EdgeCount")
                    .HasColumnType("int");

                builder.Property<int>("EndpointCount")
                    .HasColumnType("int");

                builder.Property<int>("FileCount")
                    .HasColumnType("int");

                builder.Property<int>("NodeCount")
                    .HasColumnType("int");

                builder.Property<Guid>("ProjectId")
                    .HasColumnType("char(36)");

                builder.Property<string>("Summary")
                    .IsRequired()
                    .HasMaxLength(1200)
                    .HasColumnType("varchar(1200)");

                builder.HasKey("Id");

                builder.HasIndex("CreatedAt");

                builder.HasIndex("ProjectId");

                builder.ToTable("Analyses");
            });

            modelBuilder.Entity("AiCodeAssistant.Domain.Persistence.Project", builder =>
            {
                builder.Property<Guid>("Id")
                    .HasColumnType("char(36)");

                builder.Property<DateTime>("CreatedAt")
                    .HasColumnType("datetime(6)");

                builder.Property<string>("FrameworkType")
                    .IsRequired()
                    .HasMaxLength(120)
                    .HasColumnType("varchar(120)");

                builder.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("varchar(200)");

                builder.Property<string>("SourceIdentifier")
                    .IsRequired()
                    .HasMaxLength(600)
                    .HasColumnType("varchar(600)");

                builder.Property<DateTime>("UpdatedAt")
                    .HasColumnType("datetime(6)");

                builder.HasKey("Id");

                builder.HasIndex("SourceIdentifier")
                    .IsUnique();

                builder.ToTable("Projects");
            });

            modelBuilder.Entity("AiCodeAssistant.Domain.Persistence.Analysis", builder =>
            {
                builder.HasOne("AiCodeAssistant.Domain.Persistence.Project", "Project")
                    .WithMany("Analyses")
                    .HasForeignKey("ProjectId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                builder.Navigation("Project");
            });

            modelBuilder.Entity("AiCodeAssistant.Domain.Persistence.Project", builder =>
            {
                builder.Navigation("Analyses");
            });
        }
    }
}
