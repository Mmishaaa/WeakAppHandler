using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Configurations;

public sealed class MeterEntityConfiguration : IEntityTypeConfiguration<MeterEntity>
{
    public void Configure(EntityTypeBuilder<MeterEntity> builder)
    {
        builder.ToTable("meters");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Location).HasMaxLength(64);
        builder.Property(m => m.MeterType).HasMaxLength(32);
    }
}
