using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).HasMaxLength(512).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(8192);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.AssignedAgent).HasMaxLength(128);
        builder.Property(t => t.AssignedWorker).HasMaxLength(128);
        builder.Property(t => t.GitBranch).HasMaxLength(256);
        builder.Property(t => t.Dependencies).HasColumnType("jsonb");
        builder.Property(t => t.FilesTouched).HasColumnType("jsonb");
        builder.Property(t => t.LockedFiles).HasColumnType("jsonb");

        builder.HasIndex(t => new { t.ProjectId, t.Status });
    }
}
