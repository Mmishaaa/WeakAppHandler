namespace WeakAppHandler.Processor.Infrastructure.Retention;

/// <summary>Rolls up and prunes data older than the configured retention window (TASK-048).</summary>
public interface IRetentionJob
{
    public Task<RetentionResult> RunAsync(CancellationToken cancellationToken);
}
