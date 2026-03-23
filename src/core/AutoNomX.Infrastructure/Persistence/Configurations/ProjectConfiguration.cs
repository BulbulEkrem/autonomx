using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(256).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4096);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.RepositoryPath).HasMaxLength(1024);
        builder.Property(p => p.Config).HasColumnType("jsonb");

        builder.HasMany(p => p.Tasks).WithOne(t => t.Project).HasForeignKey(t => t.ProjectId);
        builder.HasMany(p => p.PipelineRuns).WithOne(r => r.Project).HasForeignKey(r => r.ProjectId);
        builder.HasMany(p => p.Files).WithOne(f => f.Project).HasForeignKey(f => f.ProjectId);
        builder.HasMany(p => p.ChangeLogs).WithOne(c => c.Project).HasForeignKey(c => c.ProjectId);
    }
}
