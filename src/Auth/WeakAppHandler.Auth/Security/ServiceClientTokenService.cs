using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using WeakAppHandler.Auth.Domain;

namespace WeakAppHandler.Auth.Security;

/// <summary>
/// Issues RS256-signed access tokens for the client-credentials grant (TASK-010). Reuses
/// <see cref="JwtTokenService.Issuer"/>/<see cref="JwtTokenService.Audience"/> and
/// <see cref="SigningKeyProvider"/> so machine tokens validate through the same JWKS endpoint as
/// user tokens, but has its own claim shape: subject is the client id, no role claim, and a
/// space-separated "scope" claim (the OAuth2 convention) instead.
/// </summary>
public sealed class ServiceClientTokenService(SigningKeyProvider signingKeyProvider, TimeProvider timeProvider, JwtTokenService userTokenService)
{
    public TimeSpan AccessTokenLifetime => userTokenService.AccessTokenLifetime;

    public string CreateAccessToken(ServiceClient client)
    {
        var now = timeProvider.GetUtcNow();
        var scope = string.Join(' ', client.Scopes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, client.ClientId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("scope", scope),
        };

        var credentials = new SigningCredentials(signingKeyProvider.CreateSecurityKey(), SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            JwtTokenService.Issuer,
            JwtTokenService.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: now.Add(AccessTokenLifetime).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
