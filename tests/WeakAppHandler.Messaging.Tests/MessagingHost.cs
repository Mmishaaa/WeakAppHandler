using System.Globalization;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.ServiceDefaults.Messaging;

namespace WeakAppHandler.Messaging.Tests;

/// <summary>
/// A service host wired up exactly the way a real service is — through
/// <see cref="ServiceMassTransitExtensions.AddServiceMassTransit"/> and the same "RabbitMq"
/// configuration section — so the tests exercise the shipped topology rather than a bus configured
/// specially for them.
/// </summary>
internal sealed class MessagingHost : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    private readonly IHost _host;

    private MessagingHost(IHost host) => _host = host;

    public IBus Bus => _host.Services.GetRequiredService<IBus>();

    public static async Task<MessagingHost> StartAsync(
        RabbitMqIntegrationFixture fixture,
        string virtualHost,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureReceiveEndpoints = null,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var amqp = new Uri(fixture.ConnectionString);

        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = amqp.Host,
            ["RabbitMq:Port"] = amqp.Port.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:VirtualHost"] = virtualHost,
            ["RabbitMq:Username"] = RabbitMqIntegrationFixture.Username,
            ["RabbitMq:Password"] = RabbitMqIntegrationFixture.Password,
        });

        // The dead-letter test deliberately makes a consumer throw on every delivery; at the default
        // level MassTransit narrates each of those faults over several lines of test output.
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // MassTransit connects the bus in the background by default, so IHost.StartAsync returns
        // before the topology has been declared and before any endpoint is consuming — and, worse,
        // a StopAsync that arrives during that window finds the bus "Not Started", declines to stop
        // it, and leaves an orphaned bus consuming from the previous test's queues for the rest of
        // the run. Tests need the bus to be fully up when StartAsync returns, and fully down when
        // DisposeAsync returns.
        builder.Services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = StartStopTimeout;
            options.StopTimeout = StartStopTimeout;
        });

        configureServices?.Invoke(builder.Services);

        builder.AddServiceMassTransit(configureConsumers, configureReceiveEndpoints);

        var host = builder.Build();
        await host.StartAsync();

        return new MessagingHost(host);
    }

    public async ValueTask DisposeAsync()
    {
        // Stopping is what detaches the consumers from their queues; disposing alone leaves the bus
        // running long enough to swallow a message the next step of the test expects to still be there.
        await _host.StopAsync();
        _host.Dispose();
    }
}
