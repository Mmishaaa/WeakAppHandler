namespace DataProcessorService.BLL.Models;

public sealed record BatchWriteModel(IReadOnlyList<StoredReadingModel> Readings);
