using DataProcessorService.BLL.Results;

namespace DataProcessorService.BLL.Models;

public sealed record BatchWriteModel(
    BatchWriteResult Outcome,
    IReadOnlyList<StoredReadingModel> Readings)
{
    public static BatchWriteModel Duplicate { get; } = new(BatchWriteResult.Duplicate, []);
}
