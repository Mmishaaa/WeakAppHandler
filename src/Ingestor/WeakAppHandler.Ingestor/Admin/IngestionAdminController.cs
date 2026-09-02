using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using WeakAppHandler.Ingestor.Polling;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Ingestor.Admin;

/// <summary>
/// The Ingestor's admin REST surface (PRD F1/TASK-017). Machine-to-machine only: every action
/// requires a token carrying the <c>ingestion:admin</c> scope, which the Auth Service issues to the
/// seeded service client through the client-credentials grant, never to a browser user.
/// </summary>
[ApiController]
[Route("api/v1/ingestion")]
[Authorize(Policy = ServicePolicies.IngestionAdmin)]
public sealed class IngestionAdminController(
    IngestionRuntimeState state,
    CircuitBreakerStateProvider circuitBreakerState,
    IIngestionPoller poller) : ControllerBase
{
    /// <summary>
    /// Reports what the Ingestor knows about its own polling: last outcome, failure counts per
    /// reason, circuit-breaker state and the interval currently in force.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType<IngestionStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<IngestionStatusResponse> GetStatus()
    {
        var snapshot = state.Snapshot();
        var attempt = snapshot.LastAttempt;

        // Read straight off the live pipeline rather than mirrored into IngestionRuntimeState: a
        // copy kept in step by event callbacks can only ever be a stale second source of truth.
        var circuitState = circuitBreakerState.CircuitState.ToString();

        return Ok(new IngestionStatusResponse(
            attempt?.Outcome.ToString(),
            attempt?.FetchedAt,
            snapshot.LastSuccessAt,
            attempt?.BatchId,
            attempt?.ReadingCount,
            attempt?.HttpStatus,
            attempt?.DurationMs,
            attempt?.ErrorMessage,
            snapshot.TotalPolls,
            snapshot.FailureCountsByOutcome.ToDictionary(
                entry => entry.Key.ToString(),
                entry => entry.Value,
                StringComparer.Ordinal),
            circuitState,
            (int)snapshot.PollingInterval.TotalSeconds));
    }

    /// <summary>
    /// Runs one poll now and answers with its outcome. Awaited rather than queued: the point of the
    /// endpoint is to report what the poll did, and the pipeline's total budget is already bounded
    /// below the polling interval, so the response cannot hang indefinitely.
    /// </summary>
    /// <remarks>
    /// A manual poll can overlap a scheduled one. That is deliberate and harmless: each attempt gets
    /// its own batch id and publishes its own messages, and the Processor keys idempotency on message
    /// id, so the two are independent batches rather than a conflict. The loop's own "one at a time"
    /// rule exists to stop the timer stacking cycles, not to make polling globally exclusive.
    /// </remarks>
    [HttpPost("trigger")]
    [ProducesResponseType<IngestionTriggerResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IngestionTriggerResponse>> TriggerAsync(CancellationToken cancellationToken)
    {
        var attempt = await poller.PollOnceAsync(cancellationToken);

        return Ok(new IngestionTriggerResponse(
            attempt.BatchId,
            attempt.Outcome.ToString(),
            attempt.ReadingCount,
            attempt.HttpStatus,
            attempt.DurationMs,
            attempt.ErrorMessage,
            attempt.FetchedAt));
    }

    /// <summary>
    /// Changes the polling interval for the rest of this process's life. In-memory by design: the
    /// Ingestor has no database, and a restart is expected to return to the configured interval
    /// rather than silently keep an override nobody remembers making.
    /// </summary>
    [HttpPut("config")]
    [ProducesResponseType<IngestionConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<IngestionConfigResponse> UpdateConfig(IngestionConfigRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!state.TrySetPollingInterval(request.PollingIntervalSeconds, out var error))
        {
            ModelState.AddModelError(nameof(request.PollingIntervalSeconds), error!);
            return ValidationProblem(ModelState);
        }

        return Ok(new IngestionConfigResponse((int)state.PollingInterval.TotalSeconds));
    }
}
