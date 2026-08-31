using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Configurations;

public sealed class ReadingEntityConfiguration : IEntityTypeConfiguration<ReadingEntity>
{
    public void Configure(EntityTypeBuilder<ReadingEntity> builder)
    {
        builder.ToTable("readings");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.MetricCode).HasMaxLength(32);
        builder.Property(r => r.ValueNumeric).HasPrecision(12, 4);
    }
}
