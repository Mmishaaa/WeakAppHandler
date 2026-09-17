using DataProcessorService.BLL.Models;
using DataProcessorService.BLL.Results;
using DataProcessorService.DAL.Repositories;
using Shared.Entities;
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
    public async Task<Result<BatchWriteModel>> WriteAsync(
        MeterReadingsBatchModel batch,
        CancellationToken cancellationToken)
    {
        if (batch.Readings.Count == 0)
        {
            return Result.Failure<BatchWriteModel>(ReadingBatchErrors.EmptyBatch);
        }

        await using var scope = await unitOfWorkService.CreateScopeAsync(cancellationToken);

        if (await processedMessageRepository.ExistsAsync(batch.MessageId, cancellationToken))
        {
            return Result.Success(BatchWriteModel.Duplicate);
        }

        var meters = await ResolveMetersAsync(batch, cancellationToken);

        var readings = batch.Readings
            .Select(reading => new DbReading
            {
                MeterId = meters[(reading.Location, reading.MeterType)].Id,
                MetricCode = reading.MetricCode,
                ObservedAt = batch.CapturedAt,
                ValueNumeric = reading.Numeric,
                ValueBool = reading.Flag,
            })
            .ToList();

        await readingRepository.AddRangeAsync(readings, cancellationToken);

        await processedMessageRepository.AddAsync(
            new DbProcessedMessage
            {
                MessageId = batch.MessageId,
                ProcessedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);

        await scope.CommitAsync(cancellationToken);

        var metersById = meters.Values.ToDictionary(meter => meter.Id);

        return Result.Success(new BatchWriteModel(
            BatchWriteResult.Stored,
            [.. readings.Select(reading => ToStoredModel(reading, metersById[reading.MeterId]))]));
    }

    private static StoredReadingModel ToStoredModel(DbReading reading, DbMeter meter) =>
        new(
            reading.Id,
            reading.MeterId,
            meter.Location,
            meter.MeterType,
            reading.MetricCode,
            reading.ObservedAt,
            reading.ValueNumeric,
            reading.ValueBool);

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
