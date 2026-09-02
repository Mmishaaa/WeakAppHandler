using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Converters;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Configurations;

public sealed class AlertRuleEntityConfiguration : IEntityTypeConfiguration<AlertRuleEntity>
{
    public void Configure(EntityTypeBuilder<AlertRuleEntity> builder)
    {
        builder.ToTable("alert_rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(128);
        builder.Property(r => r.Location).HasMaxLength(64);
        builder.Property(r => r.MeterType).HasMaxLength(32);
        builder.Property(r => r.MetricCode).HasMaxLength(32);

        builder.Property(r => r.Operator).HasConversion<AlertOperatorConverter>().HasMaxLength(8);
        builder.Property(r => r.Severity).HasConversion<AlertSeverityConverter>().HasMaxLength(16);

        builder.Property(r => r.ThresholdNumeric).HasPrecision(12, 4);
        builder.Property(r => r.HysteresisPercent).HasPrecision(5, 2);
    }
}
