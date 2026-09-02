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
    /// <param name="connectionString">Postgres connection string the Gateway's read-only context binds to.</param>
    /// <param name="environment">
    /// Defaults to Development, matching every test written before TASK-025 needed to care - most of
    /// what this factory builds (GraphQL query behaviour) does not depend on it. TASK-025's own tests
    /// pass "Production" to reach the introspection-disabled configuration a real deployment runs
    /// under, since <c>IHostEnvironment.IsDevelopment()</c> is the only thing that switches it.
    /// </param>
    public static WebApplicationFactory<Program> Create(string connectionString, string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .UseSetting("ConnectionStrings:Gateway", connectionString));
}
