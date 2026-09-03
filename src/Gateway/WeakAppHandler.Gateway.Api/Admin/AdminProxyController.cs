using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using WeakAppHandler.Gateway.Api.ServiceClients;

namespace WeakAppHandler.Gateway.Api.Admin;

/// <summary>
/// TASK-026: proxies the Ingestor's/Processor's admin status endpoints through the Gateway, the one
/// caller both admin surfaces were designed for (see TASK-017/TASK-021's own design notes). Each
/// response is forwarded verbatim - body, content type, and status code - rather than deserialised
/// into a Gateway-side DTO and re-serialised: that is what makes "the proxied response is the same
/// data as calling the Ingestor/Processor directly" true by construction instead of by keeping two
/// copies of the same shape in sync.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class AdminProxyController(IHttpClientFactory httpClientFactory, ServiceClientTokenProvider tokenProvider)
    : ControllerBase
{
    [HttpGet("ingestion/status")]
    [Produces("application/json")]
    public Task<IActionResult> GetIngestionStatus(CancellationToken cancellationToken) =>
        ProxyGetAsync(DownstreamServiceNames.Ingestor, "api/v1/ingestion/status", cancellationToken);

    [HttpGet("processing/stats")]
    [Produces("application/json")]
    public Task<IActionResult> GetProcessingStats(CancellationToken cancellationToken) =>
        ProxyGetAsync(DownstreamServiceNames.Processor, "api/v1/processing/stats", cancellationToken);

    private async Task<IActionResult> ProxyGetAsync(string clientName, string path, CancellationToken cancellationToken)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var client = httpClientFactory.CreateClient(clientName);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = body,
            ContentType = contentType,
        };
    }
}
