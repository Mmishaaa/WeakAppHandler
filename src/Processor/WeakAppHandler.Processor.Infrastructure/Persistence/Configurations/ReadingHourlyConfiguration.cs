using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence.Configurations;

public sealed class ReadingHourlyConfiguration : IEntityTypeConfiguration<ReadingHourly>
{
    public void Configure(EntityTypeBuilder<ReadingHourly> builder)
    {
        builder.ToTable("readings_hourly");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();

        builder.Property(r => r.MetricCode).HasMaxLength(32).IsRequired();
        builder.Property(r => r.ValueAvg).HasPrecision(12, 4);
        builder.Property(r => r.ValueMin).HasPrecision(12, 4);
        builder.Property(r => r.ValueMax).HasPrecision(12, 4);
        builder.Property(r => r.ValueSum).HasPrecision(12, 4);

        builder.HasOne<Meter>().WithMany().HasForeignKey(r => r.MeterId).IsRequired();
        builder.HasOne<Metric>().WithMany().HasForeignKey(r => r.MetricCode).IsRequired();

        builder.HasIndex(r => new { r.MeterId, r.MetricCode, r.BucketStart })
            .IsUnique()
            .HasDatabaseName("ux_readings_hourly_meter_id_metric_code_bucket_start");
    }
}
