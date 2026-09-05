using System.Globalization;
using MassTransit;
using Npgsql;
using WeakAppHandler.Contracts;
using WeakAppHandler.ServiceDefaults.Messaging;

// S4 (PRD §11): republish an already-processed message and confirm the Processor deduplicates it -
// the database must be unchanged after the repeat. Publishes through the real MassTransit client
// (not a hand-rolled HTTP publish against RabbitMQ's management API) so the wire format is
// identical to what the Ingestor itself sends; a subtly wrong envelope would otherwise be silently
// ignored by the real consumers rather than genuinely exercising deduplication.
//
// Run against a stack already up via `docker compose up` - defaults match the host-published ports
// in .env.example; override via the same-named environment variables for a non-default setup.
const int pollTimeoutSeconds = 20;

var rabbitMqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
var rabbitMqPort = ushort.Parse(
    Environment.GetEnvironmentVariable("RABBITMQ_AMQP_PORT") ?? "5672", CultureInfo.InvariantCulture);
var rabbitMqVirtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VIRTUAL_HOST") ?? "weakapphandler";

// The `ingestor` user (TASK-043) already holds exactly the configure+write rights on
// readings.exchange this tool needs, and is the natural identity for it to assume: it is
// simulating the one real service that ever publishes ReadingsIngested.
var rabbitMqUsername = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "ingestor";
var rabbitMqPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "ingestor_rmq_password";

var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
var postgresDatabase = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "weakapphandler";

// gateway_ro (TASK-043) is read-only and already granted SELECT on every table Processor's writer
// role creates - exactly the level of access this tool needs to confirm the database, never to
// change it itself.
var postgresUsername = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "gateway_ro";
var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "gateway_password";

var connectionString =
    $"Host={postgresHost};Port={postgresPort};Database={postgresDatabase};" +
    $"Username={postgresUsername};Password={postgresPassword}";

await using var dataSource = NpgsqlDataSource.Create(connectionString);

// IBusControl has no async-disposable lifecycle of its own beyond Start/StopAsync (below).
var bus = Bus.Factory.CreateUsingRabbitMq(configurator =>
{
    configurator.Host(rabbitMqHost, rabbitMqPort, rabbitMqVirtualHost, host =>
    {
        host.Username(rabbitMqUsername);
        host.Password(rabbitMqPassword);
    });

    configurator.ConfigureReadingsTopology();
});

await bus.StartAsync();

try
{
    var batchId = Guid.NewGuid();
    var messageId = Guid.NewGuid();
    var fetchedAt = DateTimeOffset.UtcNow;

    var readings = new ReadingsIngested(
        messageId,
        batchId,
        fetchedAt,
        SourceLatencyMs: 120,
        Readings: [new MeterReadingEnvelope("s4-replay-demo", "energy", """{"energy":42.0}""", "hash-s4-replay")]);

    var attempt = new IngestAttemptRecorded(
        Guid.NewGuid(),
        batchId,
        fetchedAt,
        IngestOutcome.Success,
        HttpStatus: 200,
        DurationMs: 120,
        ReadingCount: 1,
        ErrorMessage: null);

    Console.WriteLine($"Publishing the original delivery for batch {batchId} (message {messageId})...");
    await bus.Publish(attempt);
    await bus.Publish(readings);

    var firstCount = await WaitForReadingCountAsync(dataSource, batchId, expected: 1, pollTimeoutSeconds);
    if (firstCount != 1)
    {
        Console.WriteLine($"FAILED: expected the original delivery to write 1 reading row within {pollTimeoutSeconds}s, found {firstCount}. Is the Processor running?");
        return 1;
    }

    Console.WriteLine("Original delivery processed. Republishing the exact same message (same message id)...");
    await bus.Publish(readings);

    // A dedicated wait rather than an immediate check: the redelivery still has to travel through
    // RabbitMQ and be consumed before the ledger can reject it, and a too-early SELECT would read
    // stale state and report a false pass.
    await Task.Delay(TimeSpan.FromSeconds(5));
    var secondCount = await CountReadingsAsync(dataSource, batchId);

    if (secondCount == firstCount)
    {
        Console.WriteLine($"PASSED: reading count for batch {batchId} is still {secondCount} after the repeat - the Processor deduplicated it.");
        return 0;
    }

    Console.WriteLine($"FAILED: reading count for batch {batchId} changed from {firstCount} to {secondCount} after the repeat - duplicate rows were written.");
    return 1;
}
finally
{
    await bus.StopAsync();
}

static async Task<long> WaitForReadingCountAsync(
    NpgsqlDataSource dataSource, Guid batchId, long expected, int timeoutSeconds)
{
    var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
    long count;

    do
    {
        count = await CountReadingsAsync(dataSource, batchId);
        if (count >= expected)
        {
            return count;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(500));
    }
    while (DateTimeOffset.UtcNow < deadline);

    return count;
}

static async Task<long> CountReadingsAsync(NpgsqlDataSource dataSource, Guid batchId)
{
    await using var command = dataSource.CreateCommand("SELECT count(*) FROM readings WHERE batch_id = $1");
    command.Parameters.AddWithValue(batchId);

    return (long)(await command.ExecuteScalarAsync() ?? 0L);
}
