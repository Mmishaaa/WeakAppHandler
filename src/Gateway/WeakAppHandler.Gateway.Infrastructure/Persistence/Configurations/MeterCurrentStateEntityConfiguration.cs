using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Configurations;

public sealed class MeterCurrentStateEntityConfiguration : IEntityTypeConfiguration<MeterCurrentStateEntity>
{
    public void Configure(EntityTypeBuilder<MeterCurrentStateEntity> builder)
    {
        builder.ToTable("meter_current_state");
        builder.HasKey(s => new { s.MeterId, s.MetricCode });

        builder.Property(s => s.MetricCode).HasMaxLength(32);
        builder.Property(s => s.ValueNumeric).HasPrecision(12, 4);
        builder.Property(s => s.PreviousValueNumeric).HasPrecision(12, 4);
    }
}
