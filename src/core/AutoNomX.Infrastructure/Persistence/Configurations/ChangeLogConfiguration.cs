using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class ChangeLogConfiguration : IEntityTypeConfiguration<ChangeLog>
{
    public void Configure(EntityTypeBuilder<ChangeLog> builder)
    {
        builder.ToTable("change_log");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.ChangeType).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.UserMessage).HasMaxLength(8192).IsRequired();
        builder.Property(c => c.AgentResponse).HasMaxLength(8192);
        builder.Property(c => c.Decisions).HasColumnType("jsonb");

        builder.HasIndex(c => c.ProjectId);
    }
}
