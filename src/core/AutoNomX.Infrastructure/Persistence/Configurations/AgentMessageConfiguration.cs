using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class AgentMessageConfiguration : IEntityTypeConfiguration<AgentMessage>
{
    public void Configure(EntityTypeBuilder<AgentMessage> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.FromAgent).HasMaxLength(128).IsRequired();
        builder.Property(m => m.ToAgent).HasMaxLength(128).IsRequired();
        builder.Property(m => m.EventType).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb");

        builder.HasIndex(m => m.ProjectId);
        builder.HasIndex(m => m.EventType);
    }
}
