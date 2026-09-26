using Shared.Entities;

namespace DataProcessorService.DAL.Repositories;

public sealed record ReadingPageAggregate(
    IReadOnlyList<DbReading> Items,
    int TotalCount);
