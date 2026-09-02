using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Application.Stats;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// TASK-021's counters in isolation, no broker or database involved: <see cref="ProcessingStatsState"/>
/// is a plain in-memory tally, so its own correctness is a pure unit test rather than an integration
/// one. The consumers actually feeding it end to end are <see cref="IngestionConsumerTests"/>'s job.
/// </summary>
public sealed class ProcessingStatsStateTests
{
    [Fact]
    public void Snapshot_Initially_IsAllZero()
    {
        var state = new ProcessingStatsState();

        var snapshot = state.Snapshot();

        Assert.Equal(0, snapshot.Processed);
        Assert.Equal(0, snapshot.Deduplicated);
        Assert.Equal(0, snapshot.DeadLettered);
    }

    [Fact]
    public void RecordResult_Recorded_IncrementsProcessedOnly()
    {
        var state = new ProcessingStatsState();

        state.RecordResult(IngestionRecordResult.Recorded);
        state.RecordResult(IngestionRecordResult.Recorded);

        var snapshot = state.Snapshot();

        Assert.Equal(2, snapshot.Processed);
        Assert.Equal(0, snapshot.Deduplicated);
        Assert.Equal(0, snapshot.DeadLettered);
    }

    [Fact]
    public void RecordResult_Duplicate_IncrementsDeduplicatedOnly()
    {
        var state = new ProcessingStatsState();

        state.RecordResult(IngestionRecordResult.Duplicate);

        var snapshot = state.Snapshot();

        Assert.Equal(0, snapshot.Processed);
        Assert.Equal(1, snapshot.Deduplicated);
        Assert.Equal(0, snapshot.DeadLettered);
    }

    [Fact]
    public void RecordDeadLettered_IncrementsDeadLetteredOnly()
    {
        var state = new ProcessingStatsState();

        state.RecordDeadLettered();
        state.RecordDeadLettered();
        state.RecordDeadLettered();

        var snapshot = state.Snapshot();

        Assert.Equal(0, snapshot.Processed);
        Assert.Equal(0, snapshot.Deduplicated);
        Assert.Equal(3, snapshot.DeadLettered);
    }

    [Fact]
    public void RecordResult_MixOfOutcomes_TalliesEachIndependently()
    {
        var state = new ProcessingStatsState();

        state.RecordResult(IngestionRecordResult.Recorded);
        state.RecordResult(IngestionRecordResult.Recorded);
        state.RecordResult(IngestionRecordResult.Duplicate);
        state.RecordDeadLettered();

        var snapshot = state.Snapshot();

        Assert.Equal(2, snapshot.Processed);
        Assert.Equal(1, snapshot.Deduplicated);
        Assert.Equal(1, snapshot.DeadLettered);
    }
}
