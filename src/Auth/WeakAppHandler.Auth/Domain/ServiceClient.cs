namespace WeakAppHandler.Auth.Domain;

public sealed class ServiceClient
{
    public required string ClientId { get; init; }

    public required string ClientSecretHash { get; set; }

    public required string[] Scopes { get; set; }
}
