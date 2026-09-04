using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WeakAppHandler.ServiceDefaults.Auth;

/// <summary>
/// JWT/JWKS authentication seam shared by every service host, with a dev-bypass switch for
/// local development without a running Auth Service - refused outright outside Development
/// (TASK-042), so no deployment can turn authentication off by configuration alone.
/// </summary>
public static class ServiceAuthenticationExtensions
{
    /// <summary>
    /// Name of the <see cref="System.Net.Http.IHttpClientFactory"/> client used to fetch the JWKS
    /// document. Named rather than a bare <c>new HttpClient()</c> so the fetch goes through the
    /// factory's handler pooling — and so an integration test can point the client at the Auth
    /// Service it is hosting in-process instead of at a URL nothing is listening on.
    /// </summary>
    public const string JwksHttpClientName = "weakapphandler-jwks";

    public static IHostApplicationBuilder AddServiceAuthentication(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var authSection = builder.Configuration.GetSection("Auth");
        var devBypassEnabled = authSection.GetValue("DevBypassEnabled", false);

        if (devBypassEnabled && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Auth:DevBypassEnabled is not allowed outside the Development environment.");
        }

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
                    options.Scopes = authSection["DevBypassScopes"] ?? options.Scopes;
                });
        }
        else
        {
            var jwksUri = authSection["JwksUri"];

            if (!string.IsNullOrWhiteSpace(jwksUri))
            {
                builder.Services.AddHttpClient(JwksHttpClientName);
                builder.Services.AddSingleton(provider => new JwksKeyCache(
                    provider.GetRequiredService<IHttpClientFactory>(),
                    jwksUri));
            }

            authenticationBuilder.AddJwtBearer();
            builder.Services
                .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IServiceProvider>((options, provider) =>
                    ConfigureJwtBearer(options, authSection, provider));
        }

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(ServicePolicies.Viewer, policy => policy.RequireRole("Viewer", "Admin"))
            .AddPolicy(ServicePolicies.Admin, policy => policy.RequireRole("Admin"))
            .AddPolicy(ServicePolicies.IngestionAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasScope(context.User, ServicePolicies.IngestionAdminScope)));

        return builder;
    }

    /// <summary>
    /// Whether the principal was granted <paramref name="requiredScope"/>. OAuth2 puts every granted
    /// scope in one space-separated "scope" claim, so this is a membership test on a split string
    /// rather than a claim-value comparison — <c>RequireClaim("scope", "ingestion:admin")</c> would
    /// reject a token that also carries any other scope.
    /// </summary>
    private static bool HasScope(ClaimsPrincipal user, string requiredScope) => user
        .FindAll("scope")
        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Contains(requiredScope, StringComparer.Ordinal);

    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        IConfigurationSection authSection,
        IServiceProvider provider)
    {
        options.Authority = authSection["Authority"];
        options.Audience = authSection["Audience"];
        options.RequireHttpsMetadata = authSection.GetValue("RequireHttpsMetadata", true);
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;

        // Load-bearing whenever Authority is not set. The Auth Service publishes a JWKS document but
        // no OpenID discovery document, so services validate against Auth:JwksUri and never learn the
        // issuer from metadata — and issuer validation, which is on by default, then rejects every
        // token for having no configured issuer to compare against.
        var issuer = authSection["Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
        {
            options.TokenValidationParameters.ValidIssuer = issuer;
        }

        var keyCache = provider.GetService<JwksKeyCache>();
        if (keyCache is not null)
        {
            options.TokenValidationParameters.IssuerSigningKeyResolver =
                (_, _, _, _) => keyCache.GetSigningKeys();
        }
    }
}
