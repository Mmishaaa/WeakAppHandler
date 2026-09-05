using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeakAppHandler.Processor.Application.Stats;
using WeakAppHandler.Processor.Infrastructure.Retention;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Processor.Worker.Admin;

/// <summary>
/// The Processor's admin REST surface (PRD §6 F3/TASK-021). Machine-to-machine only, guarded by the
/// same <c>ingestion:admin</c>-scoped policy as the Ingestor's own admin API, since both are called
/// only by the Gateway's seeded machine client (see <see cref="ServicePolicies.IngestionAdmin"/>).
/// </summary>
[ApiController]
[Route("api/v1/processing")]
[Authorize(Policy = ServicePolicies.IngestionAdmin)]
public sealed class ProcessingAdminController(ProcessingStatsState stats, IRetentionJob retentionJob) : ControllerBase
{
    /// <summary>Reports how many messages this process has recorded, deduplicated or dead-lettered.</summary>
    [HttpGet("stats")]
    [ProducesResponseType<ProcessingStatsResponse>(StatusCodes.Status200OK)]
    public ActionResult<ProcessingStatsResponse> GetStats()
    {
        var snapshot = stats.Snapshot();

        return Ok(new ProcessingStatsResponse(snapshot.Processed, snapshot.Deduplicated, snapshot.DeadLettered));
    }

    /// <summary>
    /// Runs the retention job (TASK-048) immediately rather than waiting for its next scheduled
    /// tick - the manual-trigger path the task's own test_steps call for.
    /// </summary>
    [HttpPost("retention/run")]
    [ProducesResponseType<RetentionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RetentionResult>> RunRetention(CancellationToken cancellationToken)
    {
        var result = await retentionJob.RunAsync(cancellationToken);

        return Ok(result);
    }
}
