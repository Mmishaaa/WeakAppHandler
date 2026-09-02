using HotChocolate;
using HotChocolate.Data;
using WeakAppHandler.Gateway.Application.Alerting;
using WeakAppHandler.Gateway.Application.Readings;

namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>
/// The Gateway's GraphQL root query type (PRD F4). <c>aggregations</c>/<c>ingestionStatus</c> belong
/// to later tasks (TASK-024/026) that have not built their read models yet.
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
}
