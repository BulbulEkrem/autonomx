using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class AgentMetricsConfiguration : IEntityTypeConfiguration<AgentMetrics>
{
    public void Configure(EntityTypeBuilder<AgentMetrics> builder)
    {
        builder.ToTable("agent_metrics");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.ModelUsed).HasMaxLength(256).IsRequired();

        // AgentId can reference either agents or coder_workers, so no FK constraint
        builder.Ignore(m => m.Agent);
        builder.Property(m => m.AgentId).IsRequired();

        builder.HasIndex(m => new { m.AgentId, m.ModelUsed }).IsUnique();
    }
}
