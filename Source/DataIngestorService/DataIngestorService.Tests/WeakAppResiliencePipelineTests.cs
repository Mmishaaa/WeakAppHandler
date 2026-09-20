using System.Net;
using AwesomeAssertions;
using DataIngestorService.Clients;
using DataIngestorService.Configuration;
using Polly;
using Xunit;

namespace DataIngestorService.Tests;

public class WeakAppResiliencePipelineTests
{
    [Fact]
    public async Task RateLimited_IsNotRetried_EvenWhenTheServerAsksUsToWait()
    {
        var attempts = await CountAttemptsAsync(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                TimeSpan.FromSeconds(1));

            return response;
        });

        attempts.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task ServerErrors_AreRetried_UpToTheConfiguredBudget(HttpStatusCode status)
    {
        var attempts = await CountAttemptsAsync(() => new HttpResponseMessage(status));

        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ClientErrors_AreNotRetried()
    {
        var attempts = await CountAttemptsAsync(() => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        attempts.Should().Be(1);
    }

    [Fact]
    public async Task TransportFailures_AreRetried()
    {
        var attempts = 0;

        Func<CancellationToken, ValueTask<HttpResponseMessage>> callback = _ =>
        {
            attempts++;
            throw new HttpRequestException("connection reset");
        };

        var act = async () => await BuildPipeline().ExecuteAsync(callback);

        await act.Should().ThrowAsync<HttpRequestException>();
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task SuccessfulResponses_AreReturnedOnTheFirstAttempt()
    {
        var attempts = await CountAttemptsAsync(() => new HttpResponseMessage(HttpStatusCode.OK));

        attempts.Should().Be(1);
    }

    private static async Task<int> CountAttemptsAsync(Func<HttpResponseMessage> respond)
    {
        var attempts = 0;

        await BuildPipeline().ExecuteAsync(_ =>
        {
            attempts++;

            return ValueTask.FromResult(respond());
        });

        return attempts;
    }

    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline()
    {
        var options = new WeakAppOptions
        {
            BaseUrl = new Uri("http://weakapp:8080"),
            ApiKey = "supersecret",
            PollingIntervalSeconds = 10,
            AttemptTimeoutSeconds = 3,
            TotalTimeoutSeconds = 8,
            MaxRetryAttempts = 2,
        };

        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        WeakAppResiliencePipeline.Configure(builder, options);

        return builder.Build();
    }
}
