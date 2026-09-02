using System.Globalization;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Notification.Api.Alerting;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// The Notification service as its own Program.cs builds it - the same persistence, the same
/// consumer on the same receive endpoint of the shipped topology - against a real broker and a real
/// database. Only the dispatcher is substituted, at the seam its interface exists for: the SignalR
/// hub it will eventually reach is TASK-031, and until then there would be nothing to observe.
/// </summary>
internal sealed class NotificationHost : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    private readonly WebApplicationFactory<Program> _factory;

    private NotificationHost(WebApplicationFactory<Program> factory, RecordingAlertDispatcher dispatcher)
    {
        _factory = factory;
        Dispatcher = dispatcher;
    }

    public RecordingAlertDispatcher Dispatcher { get; }

    public IBus Bus => _factory.Services.GetRequiredService<IBus>();

    public static async Task<NotificationHost> StartAsync(IntegrationTestFixture fixture, string virtualHost)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        // Applied before the host starts: the service migrates nothing itself, and a consumer that
        // receives a reading before the tables exist would fault the message rather than skip it.
        await AlertingDatabase.MigrateAsync(fixture.Postgres.ConnectionString);

        var amqp = new Uri(fixture.RabbitMq.ConnectionString);
        var dispatcher = new RecordingAlertDispatcher();

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:Notification", fixture.Postgres.ConnectionString)
            .UseSetting("RabbitMq:Host", amqp.Host)
            .UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("RabbitMq:VirtualHost", virtualHost)
            .UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username)
            .UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password)
            .ConfigureServices(services =>
            {
                // Appended after AddAlerting's TryAdd, so this is the registration that resolves.
                services.AddSingleton<IAlertDispatcher>(dispatcher);

                // MassTransit connects in the background by default, so the host would be considered
                // started before the queues exist and a reading published straight away could be
                // routed nowhere.
                services.Configure<MassTransitHostOptions>(options =>
                {
                    options.WaitUntilStarted = true;
                    options.StartTimeout = StartStopTimeout;
                    options.StopTimeout = StartStopTimeout;
                });
            }));

        // WebApplicationFactory builds and starts the host lazily; touching Services is what forces
        // it, and with WaitUntilStarted set that call does not return until the bus is up.
        _ = factory.Services.GetRequiredService<IBus>();

        return new NotificationHost(factory, dispatcher);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
