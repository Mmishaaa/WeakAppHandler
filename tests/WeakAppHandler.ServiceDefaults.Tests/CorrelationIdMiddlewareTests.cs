using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace WeakAppHandler.ServiceDefaults.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task GeneratesCorrelationId_WhenRequestHasNoHeader()
    {
        await using var app = await BuildAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/");

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }

    [Fact]
    public async Task EchoesIncomingCorrelationId()
    {
        await using var app = await BuildAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "test-correlation-id");

        var response = await client.GetAsync("/");

        Assert.Equal("test-correlation-id", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    private static async Task<WebApplication> BuildAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.MapGet("/", () => Results.Ok());

        await app.StartAsync();
        return app;
    }
}
