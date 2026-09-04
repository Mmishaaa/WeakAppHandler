using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeakAppHandler.Gateway.Api.ServiceClients;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Gateway.Api.Admin;

/// <summary>
/// TASK-026: proxies the Ingestor's/Processor's admin status endpoints through the Gateway, the one
/// caller both admin surfaces were designed for (see TASK-017/TASK-021's own design notes). Each
/// response is forwarded verbatim - body, content type, and status code - rather than deserialised
/// into a Gateway-side DTO and re-serialised: that is what makes "the proxied response is the same
/// data as calling the Ingestor/Processor directly" true by construction instead of by keeping two
/// copies of the same shape in sync.
/// </summary>
/// <remarks>
/// TASK-042: every action here is an Administration-screen-only operation (the frontend already
/// gates the screen itself on the Admin role, TASK-040/041), so the policy sits on the type. The
/// caller's own Admin token authorizes the request; the machine token
/// <see cref="ProxySendAsync"/> attaches downstream is a separate credential entirely.
/// </remarks>
[ApiController]
[Route("api/v1")]
[Authorize(Policy = ServicePolicies.Admin)]
public sealed class AdminProxyController(IHttpClientFactory httpClientFactory, ServiceClientTokenProvider tokenProvider)
    : ControllerBase
{
    [HttpGet("ingestion/status")]
    [Produces("application/json")]
    public Task<IActionResult> GetIngestionStatus(CancellationToken cancellationToken) =>
        ProxySendAsync(DownstreamServiceNames.Ingestor, HttpMethod.Get, "api/v1/ingestion/status", content: null, cancellationToken);

    [HttpGet("processing/stats")]
    [Produces("application/json")]
    public Task<IActionResult> GetProcessingStats(CancellationToken cancellationToken) =>
        ProxySendAsync(DownstreamServiceNames.Processor, HttpMethod.Get, "api/v1/processing/stats", content: null, cancellationToken);

    /// <summary>
    /// Runs one poll now, same as calling the Ingestor directly, so the Administration screen's
    /// manual-trigger button (TASK-040) has something to call from a browser that carries no
    /// <c>ingestion:admin</c>-scoped token of its own.
    /// </summary>
    [HttpPost("ingestion/trigger")]
    [Produces("application/json")]
    public Task<IActionResult> TriggerIngestion(CancellationToken cancellationToken) =>
        ProxySendAsync(DownstreamServiceNames.Ingestor, HttpMethod.Post, "api/v1/ingestion/trigger", content: null, cancellationToken);

    /// <summary>
    /// Forwards the request body as-is (no Gateway-side DTO for the same reason
    /// <see cref="ProxySendAsync"/>'s doc comment gives for the GET proxies) so the Ingestor's own
    /// validation - and its field-level 400 - reaches the Administration screen's interval control
    /// unchanged.
    /// </summary>
    [HttpPut("ingestion/config")]
    [Produces("application/json")]
    public Task<IActionResult> UpdateIngestionConfig(CancellationToken cancellationToken)
    {
        var content = new StreamContent(Request.Body);
        if (!string.IsNullOrEmpty(Request.ContentType))
        {
            content.Headers.TryAddWithoutValidation("Content-Type", Request.ContentType);
        }

        return ProxySendAsync(DownstreamServiceNames.Ingestor, HttpMethod.Put, "api/v1/ingestion/config", content, cancellationToken);
    }

    private async Task<IActionResult> ProxySendAsync(
        string clientName, HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(method, path) { Content = content };
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
