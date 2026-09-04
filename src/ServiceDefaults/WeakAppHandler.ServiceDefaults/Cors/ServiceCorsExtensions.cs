using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WeakAppHandler.ServiceDefaults.Cors;

/// <summary>
/// CORS policy for the service hosts the browser talks to directly (Gateway, Notification, Auth —
/// PRD §9's "CORS is restricted to the frontend origin"). Not part of <see cref="ServiceDefaultsExtensions.AddServiceDefaults"/>
/// because the Ingestor's admin API has no browser caller and must not opt into it.
/// </summary>
public static class ServiceCorsExtensions
{
    public const string PolicyName = "FrontendCors";

    public static IHostApplicationBuilder AddServiceCors(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var frontendOrigin = builder.Configuration["Cors:FrontendOrigin"];

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                if (string.IsNullOrWhiteSpace(frontendOrigin))
                {
                    // No origin configured: the policy allows nothing rather than defaulting to "*",
                    // so a deployment that forgets to set Cors:FrontendOrigin fails closed.
                    return;
                }

                policy
                    .WithOrigins(frontendOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return builder;
    }
}
