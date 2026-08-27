using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence.Configurations;

public sealed class MetricConfiguration : IEntityTypeConfiguration<Metric>
{
    public void Configure(EntityTypeBuilder<Metric> builder)
    {
        builder.ToTable("metrics");

        builder.HasKey(m => m.Code);
        builder.Property(m => m.Code).HasMaxLength(32);

        builder.Property(m => m.MeterType).HasMaxLength(32).IsRequired();
        builder.Property(m => m.Unit).HasMaxLength(16).IsRequired();
        builder.Property(m => m.ValueKind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.DisplayName).HasMaxLength(64).IsRequired();

        builder.HasData(MetricSeedData.All);
    }
}
