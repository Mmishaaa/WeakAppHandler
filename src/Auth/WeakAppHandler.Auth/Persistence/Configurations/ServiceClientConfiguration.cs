using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Auth.Domain;

namespace WeakAppHandler.Auth.Persistence.Configurations;

public sealed class ServiceClientConfiguration : IEntityTypeConfiguration<ServiceClient>
{
    public void Configure(EntityTypeBuilder<ServiceClient> builder)
    {
        builder.ToTable("service_clients");

        builder.HasKey(c => c.ClientId);
        builder.Property(c => c.ClientId).HasMaxLength(64);

        builder.Property(c => c.ClientSecretHash).IsRequired();
        builder.Property(c => c.Scopes).IsRequired();

        builder.HasData(AuthSeedData.ServiceClients);
    }
}
