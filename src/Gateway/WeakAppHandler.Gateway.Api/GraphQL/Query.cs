using HotChocolate;
using HotChocolate.Data;
using WeakAppHandler.Gateway.Application.Readings;

namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>
/// The Gateway's GraphQL root query type (PRD F4). Only <c>meters</c> and <c>readings</c> are wired
/// here; <c>aggregations</c>/<c>alerts</c>/<c>alertRules</c>/<c>ingestionStatus</c> belong to later
/// tasks (TASK-024/026/032) that have not built their read models yet.
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
}
