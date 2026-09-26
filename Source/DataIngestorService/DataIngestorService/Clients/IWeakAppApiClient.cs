using DataIngestorService.Clients.Results;

namespace DataIngestorService.Clients;

internal interface IWeakAppApiClient
{
    Task<MeterFetchResult> GetMetersAsync(CancellationToken cancellationToken);
}
