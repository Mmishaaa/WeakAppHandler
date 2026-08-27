using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace WeakAppHandler.Ingestor.Tests;

internal static class TestResponses
{
    public static Func<HttpResponseMessage> BadGateway() => () => new HttpResponseMessage(HttpStatusCode.BadGateway);

    public static Func<HttpResponseMessage> Success(string json) => () => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    public static Func<HttpResponseMessage> RateLimited(TimeSpan retryAfter) => () =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
        return response;
    };
}
