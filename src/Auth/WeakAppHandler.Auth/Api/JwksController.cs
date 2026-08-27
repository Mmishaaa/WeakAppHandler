using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using WeakAppHandler.Auth.Security;

namespace WeakAppHandler.Auth.Api;

[ApiController]
public sealed class JwksController(SigningKeyProvider signingKeyProvider) : ControllerBase
{
    [HttpGet("/.well-known/jwks.json")]
    public IActionResult Get()
    {
        var parameters = signingKeyProvider.Rsa.ExportParameters(includePrivateParameters: false);

        var jwk = new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            kid = signingKeyProvider.KeyId,
            n = Base64UrlEncoder.Encode(parameters.Modulus),
            e = Base64UrlEncoder.Encode(parameters.Exponent),
        };

        return Ok(new { keys = new[] { jwk } });
    }
}
