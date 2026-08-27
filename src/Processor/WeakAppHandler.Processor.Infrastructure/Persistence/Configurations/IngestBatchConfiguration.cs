using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence.Configurations;

public sealed class IngestBatchConfiguration : IEntityTypeConfiguration<IngestBatch>
{
    public void Configure(EntityTypeBuilder<IngestBatch> builder)
    {
        builder.ToTable("ingest_batches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Outcome).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(b => b.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(b => b.FetchedAt)
            .HasDatabaseName("ix_ingest_batches_fetched_at");
    }
}
