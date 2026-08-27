using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WeakAppHandler.Auth.Domain;

namespace WeakAppHandler.Auth.Security;

/// <summary>
/// Issues RS256-signed access tokens for authenticated users. The machine-to-machine
/// client-credentials grant (<see cref="ServiceClientTokenService"/>) reuses <see cref="Issuer"/>/
/// <see cref="Audience"/> and <see cref="SigningKeyProvider"/> but has its own claim shape.
/// </summary>
public sealed class JwtTokenService(SigningKeyProvider signingKeyProvider, TimeProvider timeProvider, IOptions<AuthTokenOptions> options)
{
    public const string Issuer = "weakapphandler-auth";
    public const string Audience = "weakapphandler";

    public TimeSpan AccessTokenLifetime => options.Value.AccessTokenLifetime;

    public string CreateUserAccessToken(User user)
    {
        var now = timeProvider.GetUtcNow();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("role", RoleClaims.ToClaimValue(user.Role)),
        };

        var credentials = new SigningCredentials(signingKeyProvider.CreateSecurityKey(), SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: now.Add(AccessTokenLifetime).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
