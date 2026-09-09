using DataProcessorService.BLL.Models;
using DataProcessorService.BLL.Results;
using Shared.Results;

namespace DataProcessorService.BLL.Services;

public interface IReadingBatchService
{
    Task<Result<BatchWriteResult>> WriteAsync(
        MeterReadingsBatchModel batch,
        CancellationToken cancellationToken);
}
