using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Configurations;

public sealed class AggregationBucketRowEntityConfiguration : IEntityTypeConfiguration<AggregationBucketRowEntity>
{
    public void Configure(EntityTypeBuilder<AggregationBucketRowEntity> builder)
    {
        // No backing table: rows only ever come from GetAggregationsAsync's FromSqlInterpolated call.
        builder.HasNoKey();
        builder.ToView(null);
    }
}
