using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// Builds the real Gateway.Api host (same Program.cs the service actually runs) against a real
/// PostgreSQL container, matching the precedent set by Auth.Tests' <c>WebApplicationFactory&lt;Program&gt;</c>
/// usage rather than hand-mirroring the wiring.
/// </summary>
internal static class GatewayApiFactory
{
    public static WebApplicationFactory<Program> Create(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:Gateway", connectionString));
}
