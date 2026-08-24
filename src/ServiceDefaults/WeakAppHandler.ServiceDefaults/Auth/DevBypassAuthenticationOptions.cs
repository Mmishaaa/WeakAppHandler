using Microsoft.AspNetCore.Authentication;

namespace WeakAppHandler.ServiceDefaults.Auth;

public sealed class DevBypassAuthenticationOptions : AuthenticationSchemeOptions
{
    public string UserName { get; set; } = "dev-user";

    public string Role { get; set; } = "Admin";
}
