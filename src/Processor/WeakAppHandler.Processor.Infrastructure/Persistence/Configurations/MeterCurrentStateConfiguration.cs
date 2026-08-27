using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence.Configurations;

public sealed class MeterCurrentStateConfiguration : IEntityTypeConfiguration<MeterCurrentState>
{
    public void Configure(EntityTypeBuilder<MeterCurrentState> builder)
    {
        builder.ToTable("meter_current_state");

        builder.HasKey(s => new { s.MeterId, s.MetricCode });

        builder.Property(s => s.MetricCode).HasMaxLength(32).IsRequired();
        builder.Property(s => s.ValueNumeric).HasPrecision(12, 4);
        builder.Property(s => s.PreviousValueNumeric).HasPrecision(12, 4);

        builder.HasOne<Meter>().WithMany().HasForeignKey(s => s.MeterId).IsRequired();
        builder.HasOne<Metric>().WithMany().HasForeignKey(s => s.MetricCode).IsRequired();
    }
}
