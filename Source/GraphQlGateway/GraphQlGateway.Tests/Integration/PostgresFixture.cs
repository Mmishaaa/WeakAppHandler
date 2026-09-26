using GraphQlGateway.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Shared.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace GraphQlGateway.Tests.Integration;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public IDbContextFactory<GatewayDbContext> DbContextFactory { get; private set; } = null!;

    public DateTimeOffset Origin { get; } = new(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        DbContextFactory = new PooledDbContextFactory<GatewayDbContext>(
            new DbContextOptionsBuilder<GatewayDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .Options);

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        await dbContext.Database.EnsureCreatedAsync();
        await SeedAsync(dbContext);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private static DbMeter Meter(string location, string meterType) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Location = location,
            MeterType = meterType,
            FirstSeenAt = DateTimeOffset.UnixEpoch,
            LastSeenAt = DateTimeOffset.UnixEpoch,
        };

    private async Task SeedAsync(GatewayDbContext dbContext)
    {
        var kitchen = Meter("Kitchen", "air_quality");
        var office = Meter("Office", "air_quality");
        var bedroom = Meter("Bedroom", "motion");

        dbContext.Meters.AddRange(kitchen, office, bedroom);

        dbContext.Readings.AddRange(
            Numeric(kitchen, "co2", hour: 0, 400m),
            Numeric(kitchen, "co2", hour: 0, 600m),
            Numeric(kitchen, "co2", hour: 1, 800m),
            Numeric(office, "co2", hour: 0, 900m),
            Numeric(office, "co2", hour: 1, 1100m),
            Flag(bedroom, hour: 0, detected: true),
            Flag(bedroom, hour: 0, detected: false),
            Flag(bedroom, hour: 1, detected: true));

        await dbContext.SaveChangesAsync();
    }

    private DbReading Numeric(DbMeter meter, string metricCode, int hour, decimal value) =>
        new()
        {
            MeterId = meter.Id,
            MetricCode = metricCode,
            ObservedAt = Origin.AddHours(hour),
            ValueNumeric = value,
        };

    private DbReading Flag(DbMeter meter, int hour, bool detected) =>
        new()
        {
            MeterId = meter.Id,
            MetricCode = "motion_detected",
            ObservedAt = Origin.AddHours(hour),
            ValueBool = detected,
        };
}
