using Testcontainers.RabbitMq;

namespace WeakAppHandler.IntegrationTesting;

public sealed class RabbitMqIntegrationFixture : IAsyncLifetime
{
    /// <summary>
    /// Broker credentials. Set explicitly rather than left to the Testcontainers module's defaults
    /// so tests that talk to the management HTTP API (which the AMQP connection string says nothing
    /// about) authenticate with the same pair the bus uses.
    /// </summary>
    public const string Username = "rabbitmq";

    public const string Password = "rabbitmq";

    private const ushort ManagementPort = 15672;

    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:3-management-alpine")
        .WithUsername(Username)
        .WithPassword(Password)

        // The module only publishes the AMQP port. The management HTTP API is published too because
        // TASK-012's acceptance criteria are phrased in terms of what the management UI shows, and
        // that API is the same view the UI renders — AMQP alone cannot report, for example, how many
        // messages a queue that no test is consuming from currently holds.
        .WithPortBinding(ManagementPort, true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Uri ManagementBaseAddress =>
        new($"http://{_container.Hostname}:{_container.GetMappedPublicPort(ManagementPort)}");

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Stops and restarts the broker application inside the running container, which drops every
    /// connection and rebuilds the broker's state from disk — exactly what a message has to
    /// survive to count as persistent. Restarting the container itself is not equivalent here:
    /// Testcontainers would republish the AMQP port, invalidating the fixture's connection string.
    /// </summary>
    public async Task RestartBrokerAsync()
    {
        await RunRabbitMqCtlAsync("stop_app").ConfigureAwait(false);
        await RunRabbitMqCtlAsync("start_app").ConfigureAwait(false);
        await RunRabbitMqCtlAsync("await_startup").ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an empty virtual host that <see cref="Username"/> has full permissions on, so a test
    /// whose entity names are fixed can still assert exact queue depths without another test's
    /// messages counting towards them. Done over rabbitmqctl rather than the management HTTP API
    /// because the API's vhost and permission writes are applied behind the response: granting
    /// permissions immediately after creating a vhost that way loses the grant often enough that a
    /// bus connecting straight afterwards is refused with ACCESS_REFUSED.
    /// </summary>
    public async Task CreateVirtualHostAsync(string virtualHost)
    {
        await RunRabbitMqCtlAsync("add_vhost", virtualHost).ConfigureAwait(false);
        await RunRabbitMqCtlAsync("set_permissions", "-p", virtualHost, Username, ".*", ".*", ".*")
            .ConfigureAwait(false);
    }

    public Task DeleteVirtualHostAsync(string virtualHost) => RunRabbitMqCtlAsync("delete_vhost", virtualHost);

    private async Task RunRabbitMqCtlAsync(params string[] arguments)
    {
        var result = await _container.ExecAsync(["rabbitmqctl", .. arguments]).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"rabbitmqctl {string.Join(' ', arguments)} failed with exit code {result.ExitCode}: {result.Stderr}");
        }
    }
}
