namespace WeakAppHandler.Auth;

public sealed class AuthTokenOptions
{
    public const string SectionName = "Auth:Tokens";

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeDays { get; set; } = 7;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenLifetimeMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenLifetimeDays);
}
