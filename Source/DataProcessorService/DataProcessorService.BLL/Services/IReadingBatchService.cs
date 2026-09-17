using DataProcessorService.BLL.Models;
using Shared.Results;

namespace DataProcessorService.BLL.Services;

public interface IReadingBatchService
{
    Task<Result<BatchWriteModel>> WriteAsync(
        MeterReadingsBatchModel batch,
        CancellationToken cancellationToken);
}
