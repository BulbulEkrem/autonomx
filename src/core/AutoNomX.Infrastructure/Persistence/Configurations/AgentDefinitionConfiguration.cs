using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class AgentDefinitionConfiguration : IEntityTypeConfiguration<AgentDefinition>
{
    public void Configure(EntityTypeBuilder<AgentDefinition> builder)
    {
        builder.ToTable("agents");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(128).IsRequired();
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.Model).HasMaxLength(256).IsRequired();
        builder.Property(a => a.Provider).HasMaxLength(64).IsRequired();
        builder.Property(a => a.LlmConfig).HasColumnType("jsonb");

        builder.HasMany(a => a.Histories).WithOne(h => h.Agent).HasForeignKey(h => h.AgentId);

        builder.HasIndex(a => a.Name).IsUnique();
        builder.HasIndex(a => a.Type);
    }
}
