using Microsoft.AspNetCore.Authentication;

namespace WeakAppHandler.ServiceDefaults.Auth;

public sealed class DevBypassAuthenticationOptions : AuthenticationSchemeOptions
{
    public string UserName { get; set; } = "dev-user";

    public string Role { get; set; } = "Admin";

    /// <summary>
    /// Space-separated scopes granted to the dev principal, in the same shape the Auth Service's
    /// machine tokens use. Without this, scope-based policies such as
    /// <see cref="ServicePolicies.IngestionAdmin"/> would be unreachable under the dev bypass even
    /// though role-based ones are wide open, which is a confusing local-development failure rather
    /// than a security boundary — the bypass already authenticates every request as an admin.
    /// </summary>
    public string Scopes { get; set; } = ServicePolicies.IngestionAdminScope;
}
