using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Converters;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Configurations;

public sealed class AlertEntityConfiguration : IEntityTypeConfiguration<AlertEntity>
{
    public void Configure(EntityTypeBuilder<AlertEntity> builder)
    {
        builder.ToTable("alerts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Location).HasMaxLength(64);
        builder.Property(a => a.MeterType).HasMaxLength(32);
        builder.Property(a => a.MetricCode).HasMaxLength(32);

        builder.Property(a => a.Status).HasConversion<AlertStatusConverter>().HasMaxLength(16);
        builder.Property(a => a.Severity).HasConversion<AlertSeverityConverter>().HasMaxLength(16);

        builder.Property(a => a.TriggeredValueNumeric).HasPrecision(12, 4);
        builder.Property(a => a.ResolvedValueNumeric).HasPrecision(12, 4);
    }
}
