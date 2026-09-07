using DataIngestorService.Clients.Results;

namespace DataIngestorService.Clients;

interface IWeakAppApiClient
{
    Task<MeterFetchResult> GetMetersAsync(CancellationToken cancellationToken);
}
