using MassTransit;
using Microsoft.Extensions.Logging;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.Ingestor.Polling;

/// <summary>
/// One poll attempt: call WeakApp through the resilience pipeline, then publish what happened.
/// Every attempt — successful or not — publishes an <see cref="IngestAttemptRecorded"/>, because
/// the Ingestor has no database access and that message is the only way the Processor learns of an
/// attempt at all (see tasks.json <c>architecture_review</c>). Only a successful, well-formed
/// response additionally publishes <see cref="ReadingsIngested"/>, carrying the same batch id.
/// </summary>
public sealed partial class IngestionPoller(
    IWeakAppClient weakAppClient,
    IPublishEndpoint publishEndpoint,
    TimeProvider timeProvider,
    ILogger<IngestionPoller> logger) : IIngestionPoller
{
    /// <summary>
    /// <c>ingest_batches.error_message</c> is documented as truncated (PRD §7.1), and an unbounded
    /// upstream message would otherwise be copied verbatim into every message and log line.
    /// </summary>
    private const int MaxErrorMessageLength = 1024;

    public async Task<IngestAttemptRecorded> PollOnceAsync(CancellationToken cancellationToken)
    {
        // One batch id per attempt, shared by both messages this attempt can produce, so the
        // Processor can tie the readings it stores to the ingest_batches row for the same poll.
        var batchId = NewId.NextGuid();

        var result = await weakAppClient.GetMetersAsync(cancellationToken).ConfigureAwait(false);

        // Taken after the call returns: fetchedAt is the instant the response was received (F2),
        // and the Processor uses it as observed_at because the source carries no timestamp itself.
        var fetchedAt = timeProvider.GetUtcNow();
        var durationMs = (int)Math.Round(result.Duration.TotalMilliseconds);
        var succeeded = result.Outcome == IngestOutcome.Success;
        var readings = succeeded ? MeterReadingEnvelopeMapper.Map(result.Meters) : [];

        if (succeeded)
        {
            // Published before the attempt record on purpose. Neither publish is transactional, so
            // one of the two can be lost; readings that arrive without their batch are recoverable,
            // whereas a batch claiming a reading count whose readings never arrive is a permanent
            // lie about what was ingested. The attempt record is therefore the commit marker.
            await publishEndpoint.Publish(
                new ReadingsIngested(NewId.NextGuid(), batchId, fetchedAt, durationMs, readings),
                cancellationToken).ConfigureAwait(false);
        }

        var attempt = new IngestAttemptRecorded(
            NewId.NextGuid(),
            batchId,
            fetchedAt,
            result.Outcome,
            result.HttpStatusCode,
            durationMs,
            readings.Count,
            Truncate(result.ErrorMessage));

        await publishEndpoint.Publish(attempt, cancellationToken).ConfigureAwait(false);

        LogAttemptPublished(logger, attempt.Outcome, attempt.BatchId, attempt.ReadingCount, attempt.DurationMs);

        return attempt;
    }

    private static string? Truncate(string? errorMessage) =>
        errorMessage is null || errorMessage.Length <= MaxErrorMessageLength
            ? errorMessage
            : errorMessage[..MaxErrorMessageLength];

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Poll attempt {Outcome} published for batch {BatchId} with {ReadingCount} readings in {DurationMs}ms")]
    private static partial void LogAttemptPublished(
        ILogger logger,
        IngestOutcome outcome,
        Guid batchId,
        int readingCount,
        int durationMs);
}
