using System.ComponentModel.DataAnnotations;

namespace WeakAppHandler.Gateway.Api.ServiceClients;

/// <summary>
/// The machine client credentials this Gateway authenticates itself with (TASK-026) when calling
/// the Ingestor's/Processor's admin REST APIs - the same seeded <c>gateway-ingestor</c> client
/// TASK-017/TASK-021 already granted <c>ingestion:admin</c>, reused here rather than a dedicated
/// Gateway client for the same reason TASK-021's own design note gives: one client, one scope, no
/// second Auth Service data migration for a distinction the PRD does not ask for.
/// </summary>
public sealed class ServiceClientOptions
{
    public const string SectionName = "ServiceClient";

    [Required]
    public required Uri TokenUri { get; init; }

    [Required]
    public required string ClientId { get; init; }

    [Required]
    public required string ClientSecret { get; init; }
}
