using System.Linq.Expressions;
using GraphQlGateway.BLL.Models;
using Shared.Entities;

namespace GraphQlGateway.BLL.Projections;

public static class ReadingProjections
{
    public static Expression<Func<DbReading, ReadingModel>> Projection { get; } =
        reading => new ReadingModel
        {
            Id = reading.Id,
            MeterId = reading.MeterId,
            Location = reading.Meter.Location,
            MeterType = reading.Meter.MeterType,
            MetricCode = reading.MetricCode,
            ObservedAt = reading.ObservedAt,
            Numeric = reading.ValueNumeric,
            Flag = reading.ValueBool,
        };

    private static Func<DbReading, ReadingModel> Map { get; } = Projection.Compile();

    extension(IQueryable<DbReading> readings)
    {
        public IQueryable<ReadingModel> ToModels() => readings.Select(Projection);

        public IQueryable<DbReading> ApplyFilter(ReadingFilterModel? filter)
        {
            if (filter is null)
            {
                return readings;
            }

            var query = readings;

            if (filter.MeterId is { } meterId)
            {
                query = query.Where(reading => reading.MeterId == meterId);
            }

            if (filter.Location is { Length: > 0 } location)
            {
                query = query.Where(reading => reading.Meter.Location == location);
            }

            if (filter.MeterType is { Length: > 0 } meterType)
            {
                query = query.Where(reading => reading.Meter.MeterType == meterType);
            }

            if (filter.MetricCode is { Length: > 0 } metricCode)
            {
                query = query.Where(reading => reading.MetricCode == metricCode);
            }

            if (filter.From is { } from)
            {
                query = query.Where(reading => reading.ObservedAt >= from);
            }

            if (filter.To is { } to)
            {
                query = query.Where(reading => reading.ObservedAt < to);
            }

            return query;
        }
    }

    extension(IEnumerable<DbReading> readings)
    {
        public IReadOnlyList<ReadingModel> ToModelList() => [.. readings.Select(Map)];
    }
}
