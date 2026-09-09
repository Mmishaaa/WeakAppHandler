using DataProcessorService.BLL.Models;
using DataProcessorService.BLL.Results;
using DataProcessorService.DAL.Entities;
using DataProcessorService.DAL.Repositories;
using Shared.Results;
using Shared.UnitOfWork;

namespace DataProcessorService.BLL.Services;

public sealed class ReadingBatchService(
    IMeterRepository meterRepository,
    IReadingRepository readingRepository,
    IProcessedMessageRepository processedMessageRepository,
    IUnitOfWorkService unitOfWorkService)
    : IReadingBatchService
{
    public async Task<Result<BatchWriteResult>> WriteAsync(
        MeterReadingsBatchModel batch,
        CancellationToken cancellationToken)
    {
        if (batch.Readings.Count == 0)
        {
            return Result.Failure<BatchWriteResult>(ReadingBatchErrors.EmptyBatch);
        }

        await using var scope = await unitOfWorkService.CreateScopeAsync(cancellationToken);

        if (await processedMessageRepository.ExistsAsync(batch.MessageId, cancellationToken))
        {
            return Result.Success(BatchWriteResult.Duplicate);
        }

        var meters = await ResolveMetersAsync(batch, cancellationToken);

        await readingRepository.AddRangeAsync(
            batch.Readings.Select(reading => new DbReading
            {
                MeterId = meters[(reading.Location, reading.MeterType)].Id,
                MetricCode = reading.MetricCode,
                ObservedAt = batch.CapturedAt,
                ValueNumeric = reading.Numeric,
                ValueBool = reading.Flag,
            }),
            cancellationToken);

        await processedMessageRepository.AddAsync(
            new DbProcessedMessage
            {
                MessageId = batch.MessageId,
                ProcessedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return Result.Success(BatchWriteResult.Stored);
    }

    private async Task<Dictionary<(string Location, string MeterType), DbMeter>> ResolveMetersAsync(
        MeterReadingsBatchModel batch,
        CancellationToken cancellationToken)
    {
        var keys = batch.Readings
            .Select(reading => (reading.Location, reading.MeterType))
            .Distinct()
            .ToList();

        var locations = keys.Select(key => key.Location).Distinct().ToList();

        var existing = await meterRepository.GetByLocationsAsync(locations, cancellationToken);

        var meters = existing.ToDictionary(meter => (meter.Location, meter.MeterType));

        foreach (var key in keys)
        {
            if (meters.TryGetValue(key, out var meter))
            {
                meter.LastSeenAt = batch.CapturedAt;
                continue;
            }

            var registered = new DbMeter
            {
                Id = Guid.CreateVersion7(),
                Location = key.Location,
                MeterType = key.MeterType,
                FirstSeenAt = batch.CapturedAt,
                LastSeenAt = batch.CapturedAt,
            };

            await meterRepository.AddAsync(registered, cancellationToken);
            meters[key] = registered;
        }

        return meters;
    }
}
