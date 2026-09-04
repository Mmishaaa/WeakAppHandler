using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.ServiceDefaults.Tests;

public class DevBypassAuthenticationTests
{
    [Fact]
    public async Task DevBypassEnabled_AuthenticatesAdminPolicyEndpoint_WithoutAnyToken()
    {
        await using var app = await BuildAppAsync(devBypassEnabled: true);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/admin-only");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DevBypassDisabled_RejectsAdminPolicyEndpoint_WithoutAToken()
    {
        await using var app = await BuildAppAsync(devBypassEnabled: false);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/admin-only");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void DevBypassEnabled_OutsideDevelopment_ThrowsAtStartup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:DevBypassEnabled"] = "true",
        });

        Assert.Throws<InvalidOperationException>(() => builder.AddServiceAuthentication());
    }

    private static async Task<WebApplication> BuildAppAsync(bool devBypassEnabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Explicit because a test host with no ASPNETCORE_ENVIRONMENT set defaults to Production,
        // which TASK-042's guard refuses to combine with the bypass.
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:DevBypassEnabled"] = devBypassEnabled ? "true" : "false",
        });

        builder.AddServiceAuthentication();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/admin-only", () => Results.Ok()).RequireAuthorization(ServicePolicies.Admin);

        await app.StartAsync();
        return app;
    }
}
