using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeakAppHandler.Auth.Persistence;
using WeakAppHandler.Auth.Security;

namespace WeakAppHandler.Auth.Api;

[ApiController]
public sealed class AuthController(
    AuthDbContext db,
    JwtTokenService tokenService,
    RefreshTokenService refreshTokenService,
    ServiceClientTokenService serviceClientTokenService,
    TimeProvider timeProvider,
    IOptions<AuthTokenOptions> tokenOptions) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";

    [HttpPost("/login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Unauthorized();
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user is null || !Pbkdf2PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized();
        }

        var refreshToken = await refreshTokenService.IssueAsync(user.Id, cancellationToken);
        AppendRefreshCookie(refreshToken);

        return Ok(BuildResponse(user.Role, user.Email, tokenService.CreateUserAccessToken(user)));
    }

    [HttpPost("/refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken) || string.IsNullOrEmpty(rawToken))
        {
            return Unauthorized();
        }

        var result = await refreshTokenService.RedeemAsync(rawToken, cancellationToken);
        if (result is null)
        {
            Response.Cookies.Delete(RefreshCookieName);
            return Unauthorized();
        }

        AppendRefreshCookie(result.RawToken);

        return Ok(BuildResponse(result.User.Role, result.User.Email, tokenService.CreateUserAccessToken(result.User)));
    }

    [HttpPost("/token")]
    public async Task<ActionResult<TokenResponse>> Token([FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ClientId) || string.IsNullOrEmpty(request.ClientSecret))
        {
            return Unauthorized();
        }

        var client = await db.ServiceClients.SingleOrDefaultAsync(c => c.ClientId == request.ClientId, cancellationToken);
        if (client is null || !Pbkdf2PasswordHasher.Verify(request.ClientSecret, client.ClientSecretHash))
        {
            return Unauthorized();
        }

        var accessToken = serviceClientTokenService.CreateAccessToken(client);
        return Ok(new TokenResponse(accessToken, "Bearer", (int)serviceClientTokenService.AccessTokenLifetime.TotalSeconds, string.Join(' ', client.Scopes)));
    }

    private LoginResponse BuildResponse(string role, string email, string accessToken)
        => new(accessToken, "Bearer", (int)tokenService.AccessTokenLifetime.TotalSeconds, RoleClaims.ToClaimValue(role), email);

    private void AppendRefreshCookie(string rawToken)
    {
        Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = timeProvider.GetUtcNow().Add(tokenOptions.Value.RefreshTokenLifetime),
        });
    }
}
