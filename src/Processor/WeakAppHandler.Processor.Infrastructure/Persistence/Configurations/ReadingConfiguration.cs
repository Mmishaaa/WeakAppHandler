using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence.Configurations;

public sealed class ReadingConfiguration : IEntityTypeConfiguration<Reading>
{
    public void Configure(EntityTypeBuilder<Reading> builder)
    {
        builder.ToTable("readings");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();

        builder.Property(r => r.MetricCode).HasMaxLength(32).IsRequired();
        builder.Property(r => r.ValueNumeric).HasPrecision(12, 4);

        builder.HasOne<Meter>().WithMany().HasForeignKey(r => r.MeterId).IsRequired();
        builder.HasOne<Metric>().WithMany().HasForeignKey(r => r.MetricCode).IsRequired();

        builder.HasIndex(r => new { r.MeterId, r.MetricCode, r.ObservedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_readings_meter_id_metric_code_observed_at");

        builder.HasIndex(r => r.ObservedAt)
            .HasMethod("brin")
            .HasDatabaseName("ix_readings_observed_at_brin");
    }
}
