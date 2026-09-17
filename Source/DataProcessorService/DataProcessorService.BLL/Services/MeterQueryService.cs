using DataProcessorService.BLL.Models;
using DataProcessorService.BLL.Results;
using DataProcessorService.DAL.Repositories;
using Shared.Entities;
using Shared.Results;

namespace DataProcessorService.BLL.Services;

public sealed class MeterQueryService(
    IMeterRepository meterRepository,
    IReadingRepository readingRepository)
    : IMeterQueryService
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 200;
    private const int DefaultPageSize = 50;

    public async Task<IReadOnlyList<MeterModel>> GetMetersAsync(
        string? location,
        string? meterType,
        CancellationToken cancellationToken)
    {
        var meters = await meterRepository.GetAllAsync(location, meterType, cancellationToken);

        return [.. meters.Select(ToModel)];
    }

    public async Task<Result<MeterModel>> GetMeterAsync(Guid id, CancellationToken cancellationToken)
    {
        var meter = await meterRepository.GetByIdAsync(id, cancellationToken);

        return meter is null
            ? Result.Failure<MeterModel>(MeterErrors.NotFound)
            : Result.Success(ToModel(meter));
    }

    public async Task<Result<PagedResultModel<StoredReadingModel>>> GetReadingsAsync(
        ReadingQueryModel query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var meter = await meterRepository.GetByIdAsync(query.MeterId, cancellationToken);

        if (meter is null)
        {
            return Result.Failure<PagedResultModel<StoredReadingModel>>(MeterErrors.NotFound);
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(
            query.PageSize <= 0 ? DefaultPageSize : query.PageSize,
            MinPageSize,
            MaxPageSize);

        var totalCount = await readingRepository.CountByMeterAsync(
            query.MeterId,
            query.MetricCode,
            query.From,
            query.To,
            cancellationToken);

        var readings = await readingRepository.GetByMeterAsync(
            query.MeterId,
            query.MetricCode,
            query.From,
            query.To,
            (page - 1) * pageSize,
            pageSize,
            cancellationToken);

        return Result.Success(new PagedResultModel<StoredReadingModel>(
            [.. readings.Select(reading => ToModel(reading, meter))],
            page,
            pageSize,
            totalCount));
    }

    private static MeterModel ToModel(DbMeter meter) =>
        new(meter.Id, meter.Location, meter.MeterType, meter.FirstSeenAt, meter.LastSeenAt);

    private static StoredReadingModel ToModel(DbReading reading, DbMeter meter) =>
        new(
            reading.Id,
            reading.MeterId,
            meter.Location,
            meter.MeterType,
            reading.MetricCode,
            reading.ObservedAt,
            reading.ValueNumeric,
            reading.ValueBool);
}
