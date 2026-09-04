using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using WeakAppHandler.ServiceDefaults.Cors;

namespace WeakAppHandler.ServiceDefaults.Tests;

public class ServiceCorsExtensionsTests
{
    private const string FrontendOrigin = "http://localhost:5173";

    [Fact]
    public async Task Request_FromTheConfiguredFrontendOrigin_IsAllowed()
    {
        await using var app = await BuildAppAsync();
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/ping");
        request.Headers.Add("Origin", FrontendOrigin);
        using var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins));
        Assert.Equal(FrontendOrigin, Assert.Single(allowedOrigins));
    }

    [Fact]
    public async Task Request_FromAnyOtherOrigin_ReceivesNoCorsHeaders()
    {
        await using var app = await BuildAppAsync();
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/ping");
        request.Headers.Add("Origin", "http://evil.example");
        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task<WebApplication> BuildAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:FrontendOrigin"] = FrontendOrigin,
        });

        builder.AddServiceCors();

        var app = builder.Build();
        app.UseCors(ServiceCorsExtensions.PolicyName);
        app.MapGet("/ping", () => Results.Ok());

        await app.StartAsync();
        return app;
    }
}
