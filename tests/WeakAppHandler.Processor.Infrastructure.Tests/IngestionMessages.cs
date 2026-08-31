using WeakAppHandler.Contracts;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// Builds the two messages one poll attempt produces. Locations are suffixed per test because the
/// fixture's database is shared by the whole collection and <c>meters</c> has a unique constraint on
/// (location, meter_type).
/// </summary>
internal static class IngestionMessages
{
    private const string Payload = """{"co2":512,"pm25":11,"humidity":44}""";

    public static ReadingsIngested Readings(Guid batchId, string locationPrefix, int meterCount) =>
        new(
            Guid.NewGuid(),
            batchId,
            DateTimeOffset.UtcNow,
            120,
            [.. Enumerable.Range(0, meterCount).Select(index => new MeterReadingEnvelope(
                $"{locationPrefix}-{index}",
                "air_quality",
                Payload,
                $"hash-{index}"))]);

    public static IngestAttemptRecorded Attempt(
        Guid batchId,
        IngestOutcome outcome,
        int readingCount,
        int? httpStatus = 200,
        int durationMs = 900,
        string? errorMessage = null) =>
        new(
            Guid.NewGuid(),
            batchId,
            DateTimeOffset.UtcNow,
            outcome,
            httpStatus,
            durationMs,
            readingCount,
            errorMessage);
}
