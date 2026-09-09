using Shared.Results;

namespace DataProcessorService.BLL.Results;

public static class ReadingBatchErrors
{
    public static readonly Error EmptyBatch = new(
        "reading_batch.empty",
        "The batch carries no readings, so there is nothing to store.");
}
