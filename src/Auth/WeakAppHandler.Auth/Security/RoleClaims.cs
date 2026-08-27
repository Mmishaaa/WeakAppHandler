using WeakAppHandler.Auth.Domain;

namespace WeakAppHandler.Auth.Security;

/// <summary>
/// Maps <see cref="AuthRoles"/>' lowercase database values (matching PRD §7.1's literal <c>role</c>
/// column values) to the PascalCase claim values <c>WeakAppHandler.ServiceDefaults.Auth.
/// ServicePolicies</c> expects (<c>RequireRole("Viewer", "Admin")</c>). The two must not be conflated:
/// storing the claim in the database's own casing would make every downstream role check silently
/// never grant access.
/// </summary>
public static class RoleClaims
{
    public static string ToClaimValue(string role) => role switch
    {
        AuthRoles.Viewer => "Viewer",
        AuthRoles.Admin => "Admin",
        _ => throw new InvalidOperationException($"Unknown role '{role}'."),
    };
}
