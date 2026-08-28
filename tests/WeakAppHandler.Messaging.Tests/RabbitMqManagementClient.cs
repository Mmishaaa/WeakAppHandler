using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Messaging.Tests;

/// <summary>
/// Thin wrapper over the RabbitMQ management HTTP API, which serves exactly the data the management
/// UI renders. TASK-012's acceptance criteria are written in terms of what that UI shows, so these
/// assertions read the broker's own answer instead of inferring topology from how the AMQP client
/// happens to behave — and unlike AMQP, it can report the depth of a queue nobody is consuming from.
/// The collection endpoints (/api/queues, /api/exchanges, /api/bindings) are used in preference to
/// the per-vhost ones so a vhost name never has to survive a round trip through URL escaping.
/// </summary>
internal sealed class RabbitMqManagementClient : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient _httpClient;

    public RabbitMqManagementClient(RabbitMqIntegrationFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _httpClient = new HttpClient { BaseAddress = fixture.ManagementBaseAddress };

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{RabbitMqIntegrationFixture.Username}:{RabbitMqIntegrationFixture.Password}"));

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public void Dispose() => _httpClient.Dispose();

    public async Task<ExchangeInfo?> FindExchangeAsync(string virtualHost, string name)
    {
        var exchanges = await GetArrayAsync("/api/exchanges");

        return exchanges
            .Where(e => Matches(e, virtualHost, name))
            .Select(e => new ExchangeInfo(
                name,
                e.GetProperty("type").GetString() ?? string.Empty,
                e.GetProperty("durable").GetBoolean(),
                e.GetProperty("auto_delete").GetBoolean()))
            .FirstOrDefault();
    }

    public async Task<QueueInfo?> FindQueueAsync(string virtualHost, string name)
    {
        var queues = await GetArrayAsync("/api/queues");

        return queues
            .Where(q => Matches(q, virtualHost, name))
            .Select(q => new QueueInfo(
                name,
                q.GetProperty("durable").GetBoolean(),
                q.GetProperty("auto_delete").GetBoolean(),
                ReadMessageCount(q)))
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<BindingInfo>> GetBindingsFromExchangeAsync(string virtualHost, string exchangeName)
    {
        var bindings = await GetArrayAsync("/api/bindings");

        return bindings
            .Where(b => b.GetProperty("vhost").GetString() == virtualHost
                && b.GetProperty("source").GetString() == exchangeName)
            .Select(b => new BindingInfo(
                exchangeName,
                b.GetProperty("destination").GetString() ?? string.Empty,
                b.GetProperty("destination_type").GetString() ?? string.Empty,
                b.GetProperty("routing_key").GetString() ?? string.Empty))
            .ToList();
    }

    /// <summary>
    /// Polls until <paramref name="predicate"/> holds for the named queue. Everything the broker does
    /// in response to a publish — routing, moving a faulted message to the dead-letter queue — is
    /// asynchronous, so a bare read straight after the trigger races the broker rather than testing it.
    /// </summary>
    public async Task<QueueInfo> WaitForQueueAsync(
        string virtualHost,
        string name,
        Func<QueueInfo, bool> predicate,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        QueueInfo? last = null;

        await PollAsync(
            async () =>
            {
                last = await FindQueueAsync(virtualHost, name);
                return last is not null && predicate(last);
            },
            timeout,
            () => last is null
                ? $"Queue '{name}' never appeared on vhost '{virtualHost}' within {timeout}."
                : $"Queue '{name}' never satisfied the expected condition within {timeout}; last seen: {last}.");

        return last!;
    }

    /// <summary>
    /// Waits for the management API to answer again, which it stops doing while the broker
    /// application is stopped — the plugin is part of the application that gets restarted.
    /// </summary>
    public Task WaitUntilReadyAsync(TimeSpan timeout) => PollAsync(
        async () =>
        {
            try
            {
                using var response = await _httpClient.GetAsync(Relative("/api/overview"));
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        },
        timeout,
        () => $"The management API did not become available again within {timeout}.");

    private static Uri Relative(string path) => new(path, UriKind.Relative);

    // A queue that has just been declared can be listed before its statistics are, so a missing
    // count means "nothing in it yet" rather than a malformed response.
    private static int ReadMessageCount(JsonElement queue) =>
        queue.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Number
            ? messages.GetInt32()
            : 0;

    private static bool Matches(JsonElement element, string virtualHost, string name) =>
        element.GetProperty("vhost").GetString() == virtualHost
        && element.GetProperty("name").GetString() == name;

    private static async Task PollAsync(Func<Task<bool>> condition, TimeSpan timeout, Func<string> timeoutMessage)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            if (await condition())
            {
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(timeoutMessage());
            }

            await Task.Delay(PollInterval);
        }
    }

    private async Task<IReadOnlyList<JsonElement>> GetArrayAsync(string path)
    {
        using var response = await _httpClient.GetAsync(Relative(path));
        response.EnsureSuccessStatusCode();

        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        return [.. document.EnumerateArray()];
    }
}
