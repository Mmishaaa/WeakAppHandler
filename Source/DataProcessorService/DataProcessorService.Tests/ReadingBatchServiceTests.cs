using AwesomeAssertions;
using DataProcessorService.BLL.Models;
using DataProcessorService.BLL.Results;
using DataProcessorService.BLL.Services;
using DataProcessorService.DAL.Repositories;
using Moq;
using Shared.Entities;
using Shared.UnitOfWork;
using Xunit;

namespace DataProcessorService.Tests;

public class ReadingBatchServiceTests
{
    private readonly Mock<IMeterRepository> _meters = new();
    private readonly Mock<IReadingRepository> _readings = new();
    private readonly Mock<IProcessedMessageRepository> _processed = new();
    private readonly Mock<IUnitOfWorkServiceScope> _scope = new();
    private readonly Mock<IUnitOfWorkService> _unitOfWork = new();

    public ReadingBatchServiceTests()
    {
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.CreateScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_scope.Object);

        _meters
            .Setup(meters => meters.GetByLocationsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task WriteAsync_RejectsAnEmptyBatch_WithoutTouchingTheDatabase()
    {
        var result = await Service().WriteAsync(Batch(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReadingBatchErrors.EmptyBatch);
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WriteAsync_SkipsABatchItHasAlreadyProcessed()
    {
        _processed
            .Setup(processed => processed.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Service().WriteAsync(Batch(Reading("Kitchen", "co2")), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(BatchWriteResult.Duplicate);
        result.Value.Readings.Should().BeEmpty();

        _readings.Verify(
            readings => readings.AddRangeAsync(
                It.IsAny<IEnumerable<DbReading>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _scope.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WriteAsync_RegistersAMeterPerLocationAndType_TheFirstTimeItIsSeen()
    {
        SetupNotProcessed();

        await Service().WriteAsync(
            Batch(
                Reading("Kitchen", "co2"),
                Reading("Kitchen", "pm25"),
                Reading("Office", "co2")),
            CancellationToken.None);

        _meters.Verify(
            meters => meters.AddAsync(It.IsAny<DbMeter>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task WriteAsync_ReusesAKnownMeter_AndMovesItsLastSeenForward()
    {
        SetupNotProcessed();

        var known = new DbMeter
        {
            Id = Guid.NewGuid(),
            Location = "Kitchen",
            MeterType = "air_quality",
            FirstSeenAt = DateTimeOffset.UnixEpoch,
            LastSeenAt = DateTimeOffset.UnixEpoch,
        };

        _meters
            .Setup(meters => meters.GetByLocationsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([known]);

        var capturedAt = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

        await Service().WriteAsync(
            Batch(capturedAt, Reading("Kitchen", "co2")), CancellationToken.None);

        known.LastSeenAt.Should().Be(capturedAt);
        _meters.Verify(
            meters => meters.AddAsync(It.IsAny<DbMeter>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WriteAsync_StampsEveryReading_WithTheBatchCaptureTime()
    {
        SetupNotProcessed();

        var capturedAt = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var stored = CaptureStoredReadings();

        await Service().WriteAsync(
            Batch(capturedAt, Reading("Kitchen", "co2"), Reading("Office", "co2")),
            CancellationToken.None);

        stored.Should().HaveCount(2);
        stored.Should().OnlyContain(reading => reading.ObservedAt == capturedAt);
    }

    [Fact]
    public async Task WriteAsync_RecordsTheMessage_AndCommitsOnce()
    {
        SetupNotProcessed();

        var batch = Batch(Reading("Kitchen", "co2"));

        var result = await Service().WriteAsync(batch, CancellationToken.None);

        result.Value.Outcome.Should().Be(BatchWriteResult.Stored);
        result.Value.Readings.Should().ContainSingle()
            .Which.Location.Should().Be("Kitchen");

        _processed.Verify(
            processed => processed.AddAsync(
                It.Is<DbProcessedMessage>(message => message.MessageId == batch.MessageId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _scope.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private List<DbReading> CaptureStoredReadings()
    {
        var stored = new List<DbReading>();

        _readings
            .Setup(readings => readings.AddRangeAsync(
                It.IsAny<IEnumerable<DbReading>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DbReading>, CancellationToken>((added, _) => stored.AddRange(added))
            .Returns(Task.CompletedTask);

        return stored;
    }

    private void SetupNotProcessed() =>
        _processed
            .Setup(processed => processed.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

    private ReadingBatchService Service() =>
        new(_meters.Object, _readings.Object, _processed.Object, _unitOfWork.Object);

    private static MeterReadingsBatchModel Batch(params MeterReadingModel[] readings) =>
        Batch(DateTimeOffset.UnixEpoch, readings);

    private static MeterReadingsBatchModel Batch(
        DateTimeOffset capturedAt,
        params MeterReadingModel[] readings) =>
        new(Guid.NewGuid(), Guid.NewGuid(), capturedAt, readings);

    private static MeterReadingModel Reading(string location, string metricCode) =>
        new(location, "air_quality", metricCode, Numeric: 1m, Flag: null);
}
