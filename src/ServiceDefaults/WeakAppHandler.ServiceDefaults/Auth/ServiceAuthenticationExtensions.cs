using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WeakAppHandler.ServiceDefaults.Auth;

/// <summary>
/// JWT/JWKS authentication seam shared by every service host, with a dev-bypass switch for
/// local development without a running Auth Service (finalized/locked down in TASK-042).
/// </summary>
public static class ServiceAuthenticationExtensions
{
    public static IHostApplicationBuilder AddServiceAuthentication(this IHostApplicationBuilder builder)
    {
        var authSection = builder.Configuration.GetSection("Auth");
        var devBypassEnabled = authSection.GetValue("DevBypassEnabled", false);
        var defaultScheme = devBypassEnabled
            ? DevBypassDefaults.AuthenticationScheme
            : JwtBearerDefaults.AuthenticationScheme;

        var authenticationBuilder = builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = defaultScheme;
            options.DefaultChallengeScheme = defaultScheme;
        });

        if (devBypassEnabled)
        {
            authenticationBuilder.AddScheme<DevBypassAuthenticationOptions, DevBypassAuthenticationHandler>(
                DevBypassDefaults.AuthenticationScheme,
                options =>
                {
                    options.UserName = authSection["DevBypassUserName"] ?? options.UserName;
                    options.Role = authSection["DevBypassRole"] ?? options.Role;
                });
        }
        else
        {
            authenticationBuilder.AddJwtBearer(options => ConfigureJwtBearer(options, authSection));
        }

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(ServicePolicies.Viewer, policy => policy.RequireRole("Viewer", "Admin"))
            .AddPolicy(ServicePolicies.Admin, policy => policy.RequireRole("Admin"));

        return builder;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, IConfigurationSection authSection)
    {
        options.Authority = authSection["Authority"];
        options.Audience = authSection["Audience"];
        options.RequireHttpsMetadata = authSection.GetValue("RequireHttpsMetadata", true);
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;

        var jwksUri = authSection["JwksUri"];
        if (string.IsNullOrWhiteSpace(jwksUri))
        {
            return;
        }

        var keyCache = new JwksKeyCache(new HttpClient(), jwksUri);
        options.TokenValidationParameters.IssuerSigningKeyResolver =
            (_, _, _, _) => keyCache.GetSigningKeys();
    }
}
