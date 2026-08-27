namespace WeakAppHandler.Auth.Domain;

public sealed class RefreshToken
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public required string TokenHash { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }
}
