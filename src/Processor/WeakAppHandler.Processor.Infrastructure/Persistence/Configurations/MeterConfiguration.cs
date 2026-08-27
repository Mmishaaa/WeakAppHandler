using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence.Configurations;

public sealed class MeterConfiguration : IEntityTypeConfiguration<Meter>
{
    public void Configure(EntityTypeBuilder<Meter> builder)
    {
        builder.ToTable("meters");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Location).HasMaxLength(64).IsRequired();
        builder.Property(m => m.MeterType).HasMaxLength(32).IsRequired();

        builder.HasIndex(m => new { m.Location, m.MeterType }).IsUnique();
    }
}
