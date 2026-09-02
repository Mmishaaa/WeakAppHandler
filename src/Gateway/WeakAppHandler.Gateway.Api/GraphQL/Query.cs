using HotChocolate;
using HotChocolate.Data;
using WeakAppHandler.Gateway.Application.Alerting;
using WeakAppHandler.Gateway.Application.Readings;

namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>
/// The Gateway's GraphQL root query type (PRD F4). <c>ingestionStatus</c> belongs to a later task
/// (TASK-026) that has not built its read model yet.
/// </summary>
public sealed class Query
{
    /// <summary>List meters, filterable by location and meter type (PRD F4).</summary>
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<MeterReadModel> GetMeters([Service] IGatewayReadContext context) => context.Meters;

    /// <summary>
    /// Paginated historical readings, filterable by meter, metric, location, meter type and time
    /// range (PRD F4). Ordered newest-first by default so cursors stay stable even when the caller
    /// supplies no explicit sort.
    /// </summary>
    [UsePaging(MaxPageSize = 100, DefaultPageSize = 20)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ReadingReadModel> GetReadings([Service] IGatewayReadContext context) =>
        context.Readings.OrderByDescending(r => r.ObservedAt).ThenByDescending(r => r.Id);

    /// <summary>
    /// Reverse-chronological alert feed, filterable by status/severity/location/time (PRD F4/§6.8).
    /// Reads Notification's alerting schema, not the core schema <see cref="IGatewayReadContext"/>
    /// exposes - the two are unrelated tables in the same database, owned by different services.
    /// </summary>
    [UsePaging(MaxPageSize = 100, DefaultPageSize = 20)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<AlertReadModel> GetAlerts([Service] IGatewayAlertingReadContext context) =>
        context.Alerts.OrderByDescending(a => a.TriggeredAt).ThenByDescending(a => a.Id);

    /// <summary>Configured alert rules (PRD F4), including the seed set TASK-027 applies.</summary>
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<AlertRuleReadModel> GetAlertRules([Service] IGatewayAlertingReadContext context) =>
        context.AlertRules;

    /// <summary>
    /// Bucketed aggregates (avg/min/max/sum/count) of one metric's readings over
    /// [<paramref name="from"/>, <paramref name="to"/>), grouped by location/meter type and time
    /// bucket, with every bucket in range present even when no reading fell into it (PRD F4).
    /// </summary>
    public Task<IReadOnlyList<AggregationBucketReadModel>> GetAggregations(
        string metricCode,
        AggregationBucketSize bucket,
        DateTimeOffset from,
        DateTimeOffset to,
        [Service] IGatewayReadContext context,
        string? location = null,
        string? meterType = null,
        CancellationToken cancellationToken = default) =>
        context.GetAggregationsAsync(metricCode, bucket, from, to, location, meterType, cancellationToken);
}
