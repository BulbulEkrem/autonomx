using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoNomX.Infrastructure.Persistence.Configurations;

public class CoderWorkerConfiguration : IEntityTypeConfiguration<CoderWorker>
{
    public void Configure(EntityTypeBuilder<CoderWorker> builder)
    {
        builder.ToTable("coder_workers");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Name).HasMaxLength(128).IsRequired();
        builder.Property(w => w.Model).HasMaxLength(256).IsRequired();
        builder.Property(w => w.Provider).HasMaxLength(64).IsRequired();
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasOne(w => w.CurrentTask).WithMany().HasForeignKey(w => w.CurrentTaskId);

        builder.HasIndex(w => w.Status);
    }
}
