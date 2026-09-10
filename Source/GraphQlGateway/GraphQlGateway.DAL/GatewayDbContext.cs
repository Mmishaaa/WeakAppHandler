using Microsoft.EntityFrameworkCore;
using Shared.Entities;

namespace GraphQlGateway.DAL;

public sealed class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    public DbSet<DbMeter> Meters { get; set; } = null!;

    public DbSet<DbReading> Readings { get; set; } = null!;
}
