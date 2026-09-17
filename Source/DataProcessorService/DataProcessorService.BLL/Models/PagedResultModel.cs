namespace DataProcessorService.BLL.Models;

public sealed record PagedResultModel<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
