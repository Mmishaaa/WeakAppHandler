namespace WeakAppHandler.Auth.Domain;

public sealed class User
{
    public required Guid Id { get; init; }

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public required string PasswordHash { get; set; }

    public required string Role { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
