using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// Builds the real Gateway.Api host (same Program.cs the service actually runs) against a real
/// PostgreSQL container, matching the precedent set by Auth.Tests' <c>WebApplicationFactory&lt;Program&gt;</c>
/// usage rather than hand-mirroring the wiring.
/// </summary>
internal static class GatewayApiFactory
{
    /// <param name="connectionString">Postgres connection string the Gateway's read-only contexts bind to.</param>
    /// <param name="rabbitMqConnectionString">
    /// TASK-032: since Program.cs now unconditionally starts a MassTransit bus (the
    /// onReadingStored subscription's receive endpoint), every test host needs a real broker to
    /// connect to - the shared fixture's, not an unconfigured default of localhost:5672/guest that
    /// would either hang retrying against nothing or, worse, quietly bind against a real broker
    /// left running on a developer's machine from `docker compose up`.
    /// </param>
    /// <param name="environment">
    /// Defaults to Development, matching every test written before TASK-025 needed to care - most of
    /// what this factory builds (GraphQL query behaviour) does not depend on it. TASK-025's own tests
    /// pass "Production" to reach the introspection-disabled configuration a real deployment runs
    /// under, since <c>IHostEnvironment.IsDevelopment()</c> is the only thing that switches it.
    /// </param>
    public static WebApplicationFactory<Program> Create(
        string connectionString,
        string rabbitMqConnectionString,
        string environment = "Development")
    {
        var amqp = new Uri(rabbitMqConnectionString);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .UseSetting("ConnectionStrings:Gateway", connectionString)
            .UseSetting("RabbitMq:Host", amqp.Host)
            .UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("RabbitMq:VirtualHost", "/")
            .UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username)
            .UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password));
    }
}
