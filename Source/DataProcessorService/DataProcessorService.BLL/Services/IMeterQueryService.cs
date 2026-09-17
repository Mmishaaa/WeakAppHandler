using DataProcessorService.BLL.Models;
using Shared.Results;

namespace DataProcessorService.BLL.Services;

public interface IMeterQueryService
{
    Task<IReadOnlyList<MeterModel>> GetMetersAsync(
        string? location,
        string? meterType,
        CancellationToken cancellationToken);

    Task<Result<MeterModel>> GetMeterAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<PagedResultModel<StoredReadingModel>>> GetReadingsAsync(
        ReadingQueryModel query,
        CancellationToken cancellationToken);
}
