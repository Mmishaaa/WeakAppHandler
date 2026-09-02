using System.Globalization;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.Contracts;
using WeakAppHandler.Gateway.Api.GraphQL;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-032: <c>onReadingStored</c> is fed by a real receive endpoint bound to the same
/// <c>readings.stored</c> routing key Notification consumes (TASK-029), not by a database poll -
/// proven here by publishing through a real <see cref="IBus"/> and reading off the real
/// <see cref="ITopicEventReceiver"/> HotChocolate's resolver itself would use, rather than by
/// asserting on <see cref="ReadingStoredSubscriptionConsumer"/> or <see cref="ReadingStoredTopics"/>
/// in isolation - either of those alone could pass while Program.cs never actually wired them
/// together.
/// </summary>
/// <remarks>
/// Goes through the DI-resolved topic receiver/sender rather than a graphql-ws client: the WebSocket
/// transport is HotChocolate's own tested code, not something this task adds, and driving the
/// subscription through the exact seam <see cref="Subscription.SubscribeToOnReadingStoredAsync"/>
/// itself uses is a more direct proof of the routing/filtering logic than a transport round trip
/// would be. Each test gets its own RabbitMQ virtual host, matching
/// <c>AlertingConsumerTests</c>' precedent, so two tests running concurrently against the shared
/// broker cannot see each other's deliveries on the fixed queue name the topology binds.
/// </remarks>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class ReadingStoredSubscriptionTests(IntegrationTestFixture fixture)
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task OnReadingStored_NoFilterArguments_ReceivesEveryStoredReading()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            using var factory = CreateFactory(virtualHost);
            var bus = factory.Services.GetRequiredService<IBus>();
            var receiver = factory.Services.GetRequiredService<ITopicEventReceiver>();

            var stream = await receiver.SubscribeAsync<ReadingStoredPayload>(
                ReadingStoredTopics.Resolve(location: null, meterType: null), CancellationToken.None);
            var readTask = FirstAsync(stream, DeliveryTimeout);

            var meterId = Guid.NewGuid();
            var observedAt = DateTimeOffset.UtcNow;
            await bus.Publish(new ReadingStored(
                meterId, "Kitchen", "air_quality", "co2", new MetricValue(812, null), null, true, observedAt));

            var payload = await readTask;

            Assert.Equal(meterId, payload.MeterId);
            Assert.Equal("Kitchen", payload.Location);
            Assert.Equal("air_quality", payload.MeterType);
            Assert.Equal("co2", payload.MetricCode);
            Assert.Equal(812m, payload.ValueNumeric);
            Assert.Equal(observedAt, payload.ObservedAt);
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    [Fact]
    public async Task OnReadingStored_FilteredByLocationAndMeterType_NeverReceivesAReadingForAnotherLocation()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            using var factory = CreateFactory(virtualHost);
            var bus = factory.Services.GetRequiredService<IBus>();
            var receiver = factory.Services.GetRequiredService<ITopicEventReceiver>();

            var stream = await receiver.SubscribeAsync<ReadingStoredPayload>(
                ReadingStoredTopics.Resolve("Kitchen", "air_quality"), CancellationToken.None);
            var readTask = FirstAsync(stream, DeliveryTimeout);

            // Published first, to a different location: must never reach a subscriber scoped to
            // Kitchen/air_quality, proving the filter is applied by routing, not read and discarded.
            await bus.Publish(new ReadingStored(
                Guid.NewGuid(), "Garage", "air_quality", "co2", new MetricValue(500, null), null, true, DateTimeOffset.UtcNow));

            var matchingMeterId = Guid.NewGuid();
            var observedAt = DateTimeOffset.UtcNow;
            await bus.Publish(new ReadingStored(
                matchingMeterId, "Kitchen", "air_quality", "co2", new MetricValue(900, null), null, true, observedAt));

            var payload = await readTask;

            Assert.Equal(matchingMeterId, payload.MeterId);
            Assert.Equal(900m, payload.ValueNumeric);
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    private static async Task<T> FirstAsync<T>(ISourceStream<T> stream, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        await foreach (var item in stream.ReadEventsAsync().WithCancellation(cts.Token))
        {
            return item;
        }

        throw new TimeoutException($"No event arrived on the subscription stream within {timeout}.");
    }

    /// <summary>The Gateway host, its real Program.cs wiring, against the shared Postgres container and this test's own RabbitMQ virtual host.</summary>
    private WebApplicationFactory<Program> CreateFactory(string virtualHost)
    {
        var amqp = new Uri(fixture.RabbitMq.ConnectionString);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:Gateway", fixture.Postgres.ConnectionString)
            .UseSetting("RabbitMq:Host", amqp.Host)
            .UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("RabbitMq:VirtualHost", virtualHost)
            .UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username)
            .UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password)
            .ConfigureServices(services => services.Configure<MassTransitHostOptions>(options =>
            {
                // MassTransit connects in the background by default, so the host would be
                // considered started before the queue exists and a publish right after could be
                // routed nowhere - the same reasoning as NotificationHost.
                options.WaitUntilStarted = true;
                options.StartTimeout = StartStopTimeout;
                options.StopTimeout = StartStopTimeout;
            })));

        // WebApplicationFactory builds and starts the host lazily; touching Services is what forces
        // it, and with WaitUntilStarted set that call does not return until the bus is up.
        _ = factory.Services.GetRequiredService<IBus>();

        return factory;
    }
}
