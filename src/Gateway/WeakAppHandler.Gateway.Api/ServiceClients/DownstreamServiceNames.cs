namespace WeakAppHandler.Gateway.Api.ServiceClients;

/// <summary>
/// <see cref="IHttpClientFactory"/> client names for the admin APIs this Gateway proxies (TASK-026),
/// each pre-configured with the matching service's base address.
/// </summary>
public static class DownstreamServiceNames
{
    public const string Ingestor = "weakapphandler-ingestor";

    public const string Processor = "weakapphandler-processor";
}
