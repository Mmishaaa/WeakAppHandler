using AwesomeAssertions;
using DataProcessorService.BLL.Models;
using DataProcessorService.BLL.Results;
using DataProcessorService.BLL.Services;
using DataProcessorService.DAL;
using DataProcessorService.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.UnitOfWork;
using Xunit;

namespace DataProcessorService.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class ReadingBatchServiceDatabaseTests(PostgresFixture postgres)
{
    private static int _capturedAtOffset;

    [Fact]
    public async Task WriteAsync_StoresReadings_AndRegistersTheMetersBehindThem()
    {
        var batch = Batch(
            Reading("Kitchen", "air_quality", "co2", 812m),
            Reading("Kitchen", "air_quality", "pm25", 14m),
            Reading("Office", "motion", "motion_detected", flag: true));

        await using (var dbContext = postgres.CreateDbContext())
        {
            var result = await Service(dbContext).WriteAsync(batch, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Outcome.Should().Be(BatchWriteResult.Stored);
        }

        await using var verification = postgres.CreateDbContext();

        var stored = await verification.Readings
            .Include(reading => reading.Meter)
            .Where(reading => reading.ObservedAt == batch.CapturedAt)
            .ToListAsync();

        stored.Should().HaveCount(3);
        stored.Select(reading => reading.Meter.Location).Distinct()
            .Should().BeEquivalentTo(new[] { "Kitchen", "Office" });

        stored.Single(reading => reading.MetricCode == "motion_detected")
            .ValueBool.Should().BeTrue();
        stored.Single(reading => reading.MetricCode == "co2")
            .ValueNumeric.Should().Be(812m);
    }

    [Fact]
    public async Task WriteAsync_IsIdempotent_WhenTheSameMessageArrivesTwice()
    {
        var batch = Batch(Reading("Hall", "air_quality", "co2", 500m));

        await using (var first = postgres.CreateDbContext())
        {
            await Service(first).WriteAsync(batch, CancellationToken.None);
        }

        await using (var second = postgres.CreateDbContext())
        {
            var repeat = await Service(second).WriteAsync(batch, CancellationToken.None);

            repeat.Value.Outcome.Should().Be(BatchWriteResult.Duplicate);
        }

        await using var verification = postgres.CreateDbContext();

        var count = await verification.Readings
            .CountAsync(reading => reading.ObservedAt == batch.CapturedAt);

        count.Should().Be(1);
    }

    [Fact]
    public async Task WriteAsync_ReusesAMeter_AcrossBatches()
    {
        var first = Batch(Reading("Attic", "air_quality", "co2", 400m));
        var second = Batch(Reading("Attic", "air_quality", "co2", 450m));

        await using (var dbContext = postgres.CreateDbContext())
        {
            await Service(dbContext).WriteAsync(first, CancellationToken.None);
        }

        await using (var dbContext = postgres.CreateDbContext())
        {
            await Service(dbContext).WriteAsync(second, CancellationToken.None);
        }

        await using var verification = postgres.CreateDbContext();

        var meters = await verification.Meters
            .Where(meter => meter.Location == "Attic")
            .ToListAsync();

        meters.Should().ContainSingle()
            .Which.LastSeenAt.Should().Be(second.CapturedAt);
    }

    private static ReadingBatchService Service(ProcessorDbContext dbContext) =>
        new(
            new MeterRepository(dbContext),
            new ReadingRepository(dbContext),
            new ProcessedMessageRepository(dbContext),
            new UnitOfWorkService(dbContext));

    private static MeterReadingsBatchModel Batch(params MeterReadingModel[] readings) =>
        new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            NextCapturedAt(),
            readings);

    private static DateTimeOffset NextCapturedAt() =>
        new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero)
            .AddSeconds(Interlocked.Increment(ref _capturedAtOffset));

    private static MeterReadingModel Reading(
        string location,
        string meterType,
        string metricCode,
        decimal? numeric = null,
        bool? flag = null) =>
        new(location, meterType, metricCode, numeric, flag);
}
