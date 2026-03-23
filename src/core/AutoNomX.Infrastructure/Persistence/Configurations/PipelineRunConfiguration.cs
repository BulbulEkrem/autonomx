using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class PipelineRunConfiguration : IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(EntityTypeBuilder<PipelineRun> builder)
    {
        builder.ToTable("pipeline_runs");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.CurrentStep).HasMaxLength(128).IsRequired();
        builder.Property(p => p.ErrorMessage).HasMaxLength(4096);

        builder.HasIndex(p => new { p.ProjectId, p.Status });
    }
}
