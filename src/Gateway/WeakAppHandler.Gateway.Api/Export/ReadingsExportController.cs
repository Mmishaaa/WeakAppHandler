using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Gateway.Application.Readings;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Gateway.Api.Export;

/// <summary>
/// TASK-026: <c>GET /api/v1/readings/export</c> streams every matching reading as CSV directly onto
/// the response body via <see cref="EntityFrameworkQueryableExtensions.AsAsyncEnumerable{TSource}"/>
/// - one row fetched, written, and discarded at a time - rather than materialising the result set
/// (<c>ToListAsync</c>) before writing anything, which is what keeps memory constant regardless of
/// how large the requested range is. <see cref="IGatewayReadContext.Readings"/> is already an
/// EF Core <c>IQueryable</c> over a <c>NoTracking</c> context (see <c>GatewayReadDbContext</c>), so
/// the filters below translate into the same SQL WHERE clause Postgres would run for any other query
/// against it - nothing here forces a client-side evaluation.
/// </summary>
[ApiController]
[Route("api/v1/readings")]
[Authorize(Policy = ServicePolicies.Viewer)]
public sealed class ReadingsExportController(IGatewayReadContext readContext) : ControllerBase
{
    private const string CsvHeader = "id,meterId,location,meterType,metricCode,observedAt,valueNumeric,valueBool,isChanged";

    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task Export(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? location,
        [FromQuery] string? meterType,
        [FromQuery] string? metricCode,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/csv";
        Response.Headers.ContentDisposition = "attachment; filename=\"readings.csv\"";

        var query = readContext.Readings.AsQueryable();

        if (from is not null)
        {
            query = query.Where(r => r.ObservedAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(r => r.ObservedAt < to);
        }

        if (!string.IsNullOrEmpty(location))
        {
            query = query.Where(r => r.Location == location);
        }

        if (!string.IsNullOrEmpty(meterType))
        {
            query = query.Where(r => r.MeterType == meterType);
        }

        if (!string.IsNullOrEmpty(metricCode))
        {
            query = query.Where(r => r.MetricCode == metricCode);
        }

        query = query.OrderBy(r => r.ObservedAt).ThenBy(r => r.Id);

        // leaveOpen: Response.Body is owned by the framework, which disposes it once the request
        // completes - this StreamWriter must not close it first.
        await using var writer = new StreamWriter(Response.Body, leaveOpen: true);
        await writer.WriteLineAsync(CsvHeader).ConfigureAwait(false);

        await foreach (var reading in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(FormatRow(reading)).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string FormatRow(ReadingReadModel reading) => string.Join(
        ',',
        reading.Id.ToString(CultureInfo.InvariantCulture),
        reading.MeterId.ToString(),
        CsvField(reading.Location),
        CsvField(reading.MeterType),
        CsvField(reading.MetricCode),
        reading.ObservedAt.ToString("O", CultureInfo.InvariantCulture),
        reading.ValueNumeric?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        reading.ValueBool?.ToString() ?? string.Empty,
        reading.IsChanged ? "true" : "false");

    /// <summary>Quotes a field only when it needs it (contains a comma, quote, or newline), doubling
    /// any embedded quotes - standard CSV escaping (RFC 4180). Every field here comes from a closed
    /// vocabulary (location/meterType/metricCode) that never actually contains these characters, but
    /// escaping unconditionally on the rare cases rather than assuming the vocabulary never changes
    /// is what keeps a future free-text field from silently producing a corrupt CSV.</summary>
    private static string CsvField(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
