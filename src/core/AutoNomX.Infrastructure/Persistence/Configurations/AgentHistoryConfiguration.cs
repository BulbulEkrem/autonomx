using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class AgentHistoryConfiguration : IEntityTypeConfiguration<AgentHistory>
{
    public void Configure(EntityTypeBuilder<AgentHistory> builder)
    {
        builder.ToTable("agent_history");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.AgentInstanceId).HasMaxLength(128);
        builder.Property(h => h.Role).HasMaxLength(32).IsRequired();
        builder.Property(h => h.Content).IsRequired();
        builder.Property(h => h.ModelUsed).HasMaxLength(256);

        builder.HasOne(h => h.Task).WithMany(t => t.AgentHistories).HasForeignKey(h => h.TaskId);

        builder.HasIndex(h => h.AgentId);
        builder.HasIndex(h => h.TaskId);
        builder.HasIndex(h => h.AgentInstanceId);
    }
}
