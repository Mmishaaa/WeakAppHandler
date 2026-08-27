using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Auth.Domain;

namespace WeakAppHandler.Auth.Persistence.Configurations;

public sealed class SigningKeyConfiguration : IEntityTypeConfiguration<SigningKey>
{
    public void Configure(EntityTypeBuilder<SigningKey> builder)
    {
        builder.ToTable("signing_keys");

        builder.HasKey(k => k.KeyId);
        builder.Property(k => k.KeyId).HasMaxLength(64);
        builder.Property(k => k.PrivateKeyPkcs8).IsRequired();
    }
}
