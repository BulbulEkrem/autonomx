using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class ProjectFileConfiguration : IEntityTypeConfiguration<ProjectFile>
{
    public void Configure(EntityTypeBuilder<ProjectFile> builder)
    {
        builder.ToTable("files");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Path).HasMaxLength(1024).IsRequired();
        builder.Property(f => f.ContentHash).HasMaxLength(128);
        builder.Property(f => f.LockedByWorker).HasMaxLength(128);

        builder.HasOne(f => f.Task).WithMany(t => t.Files).HasForeignKey(f => f.TaskId);

        builder.HasIndex(f => new { f.ProjectId, f.Path }).IsUnique();
    }
}
