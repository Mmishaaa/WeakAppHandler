using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WeakAppHandler.ServiceDefaults.Auth;

/// <summary>
/// Authenticates every request as a configurable dev user, for local development without a
/// running Auth Service. Must never be enabled outside Development (see TASK-042).
/// </summary>
public sealed class DevBypassAuthenticationHandler(
    IOptionsMonitor<DevBypassAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<DevBypassAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, Options.UserName),
            new(ClaimTypes.Name, Options.UserName),
            new(ClaimTypes.Role, Options.Role),
        ];

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
