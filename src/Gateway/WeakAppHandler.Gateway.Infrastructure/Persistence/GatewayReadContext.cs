using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Gateway.Application.Readings;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence;

public sealed class GatewayReadContext(GatewayReadDbContext dbContext) : IGatewayReadContext
{
    // date_trunc's unit argument and generate_series' step both accept plain text parameters in
    // Postgres - no string interpolation/concatenation into the SQL text is needed for either, so
    // this stays exactly as parameterized (and therefore as injection-safe) as every other value
    // FromSqlInterpolated below binds.
    private static readonly Dictionary<AggregationBucketSize, (string Unit, string Step)> BucketDefinitions = new()
    {
        [AggregationBucketSize.Minute] = ("minute", "1 minute"),
        [AggregationBucketSize.Hour] = ("hour", "1 hour"),
        [AggregationBucketSize.Day] = ("day", "1 day"),
    };

    public IQueryable<MeterReadModel> Meters => dbContext.Meters
        .Select(m => new MeterReadModel
        {
            Id = m.Id,
            Location = m.Location,
            MeterType = m.MeterType,
            FirstSeenAt = m.FirstSeenAt,
            LastSeenAt = m.LastSeenAt,
        });

    // The owning meter's location/meterType are joined in here rather than exposed as a nested
    // GraphQL field, so `readings(where: { location: ... })` becomes one SQL WHERE on a joined
    // column instead of a second round trip per row.
    public IQueryable<ReadingReadModel> Readings => dbContext.Readings.Join(
        dbContext.Meters,
        reading => reading.MeterId,
        meter => meter.Id,
        (reading, meter) => new ReadingReadModel
        {
            Id = reading.Id,
            MeterId = reading.MeterId,
            Location = meter.Location,
            MeterType = meter.MeterType,
            MetricCode = reading.MetricCode,
            ObservedAt = reading.ObservedAt,
            ValueNumeric = reading.ValueNumeric,
            ValueBool = reading.ValueBool,
            IsChanged = reading.IsChanged,
        });

    public async Task<IReadOnlyList<MeterCurrentValueReadModel>> GetCurrentValuesAsync(
        IReadOnlyList<Guid> meterIds,
        CancellationToken cancellationToken) =>
        await dbContext.MeterCurrentStates
            .Where(s => meterIds.Contains(s.MeterId))
            .Select(s => new MeterCurrentValueReadModel(
                s.MeterId,
                s.MetricCode,
                s.ValueNumeric,
                s.ValueBool,
                s.PreviousValueNumeric,
                s.PreviousValueBool,
                s.ObservedAt,
                s.ChangedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AggregationBucketReadModel>> GetAggregationsAsync(
        string metricCode,
        AggregationBucketSize bucket,
        DateTimeOffset from,
        DateTimeOffset until,
        string? location,
        string? meterType,
        CancellationToken cancellationToken)
    {
        var (unit, step) = BucketDefinitions[bucket];

        // series: every (location, meterType) pair a matching meter exists for, so a meter that
        // never reported this metric in range still gets a full run of zero-count buckets rather
        // than being silently absent from the result (PRD F4's "empty buckets represented
        // explicitly"). buckets: every bucket boundary in [from, until) regardless of data - the
        // "- interval '1 microsecond'" on the upper bound keeps `until` itself exclusive
        // (generate_series is otherwise inclusive of its end value, which would produce one bucket
        // too many whenever `until` lands exactly on a bucket boundary). agg: the actual GROUP BY
        // over readings, joined by Postgres to (series x buckets) so a bucket with no matching row
        // surfaces as NULL/0 rather than never appearing - all bucketing/grouping happens in this one
        // SQL statement, not after materializing rows in memory.
        var rows = await dbContext.AggregationBucketRows
            .FromSqlInterpolated(
                $"""
                WITH series AS (
                    SELECT DISTINCT m.location, m.meter_type
                    FROM meters m
                    -- Explicitly cast, not just compared: Postgres's prepared-statement parser
                    -- cannot infer a bare parameter's type from "$1 IS NULL" alone (error 42P18,
                    -- "could not determine data type of parameter") when that is the ONLY place a
                    -- given occurrence of the parameter appears - the OR's other branch binds a
                    -- SEPARATE parameter (FromSqlInterpolated gives every {location} occurrence its
                    -- own placeholder), so it gives this one no type context to borrow.
                    WHERE ({location}::text IS NULL OR m.location = {location})
                      AND ({meterType}::text IS NULL OR m.meter_type = {meterType})
                ),
                buckets AS (
                    SELECT generate_series(
                        date_trunc({unit}, {from}),
                        date_trunc({unit}, {until} - interval '1 microsecond'),
                        ({step})::interval) AS bucket_start
                ),
                agg AS (
                    SELECT
                        m.location,
                        m.meter_type,
                        date_trunc({unit}, r.observed_at) AS bucket_start,
                        AVG(r.value_numeric) AS avg_value,
                        MIN(r.value_numeric) AS min_value,
                        MAX(r.value_numeric) AS max_value,
                        SUM(r.value_numeric) AS sum_value,
                        COUNT(*) AS reading_count
                    FROM readings r
                    JOIN meters m ON m.id = r.meter_id
                    WHERE r.metric_code = {metricCode}
                      AND r.observed_at >= {from}
                      AND r.observed_at < {until}
                      AND ({location}::text IS NULL OR m.location = {location})
                      AND ({meterType}::text IS NULL OR m.meter_type = {meterType})

                    -- Grouped by the SELECT list's own bucket_start alias rather than repeating
                    -- date_trunc({unit}, r.observed_at): FromSqlInterpolated binds each occurrence
                    -- of an interpolated hole to its own parameter, so a second, textually identical
                    -- date_trunc({unit}, ...) here would bind {unit} to a DIFFERENT parameter than
                    -- the one in the SELECT list - two parameters Postgres cannot prove equal at
                    -- parse time, which it rejects with "column ... must appear in the GROUP BY
                    -- clause" despite both holding the same value at execution. Postgres allows
                    -- GROUP BY to reference an output column's own alias for exactly this reason.
                    GROUP BY m.location, m.meter_type, bucket_start
                )
                SELECT
                    s.location AS location,
                    s.meter_type AS meter_type,
                    b.bucket_start AS bucket_start,
                    agg.avg_value AS avg,
                    agg.min_value AS min,
                    agg.max_value AS max,
                    agg.sum_value AS sum,
                    COALESCE(agg.reading_count, 0) AS count
                FROM series s
                CROSS JOIN buckets b
                LEFT JOIN agg
                    ON agg.location = s.location
                   AND agg.meter_type = s.meter_type
                   AND agg.bucket_start = b.bucket_start
                ORDER BY s.location, s.meter_type, b.bucket_start
                """)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new AggregationBucketReadModel
            {
                BucketStart = r.BucketStart,
                Location = r.Location,
                MeterType = r.MeterType,
                Avg = r.Avg,
                Min = r.Min,
                Max = r.Max,
                Sum = r.Sum,
                Count = r.Count,
            })
            .ToList();
    }
}
