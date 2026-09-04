using System.Net.Http.Json;
using System.Text.Json;

namespace WeakAppHandler.Gateway.Api.Tests;

internal static class GraphQlClient
{
    public static async Task<JsonElement> PostAsync(
        HttpClient client,
        string query,
        object? variables = null)
    {
        var response = await client.PostAsJsonAsync("/graphql", new { query, variables });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// For requests a test expects the server to refuse outright rather than answer with a GraphQL
    /// "errors" array over HTTP 200 - HotChocolate returns a 4xx for document-validation failures
    /// (an execution-depth violation, introspection being disabled) rather than the 2xx it uses for
    /// argument-level validation errors, so asserting success first, as <see cref="PostAsync"/> does,
    /// would throw before the body - which is what a test actually wants to inspect - is ever read.
    /// </summary>
    public static async Task<JsonElement> PostExpectingRejectionAsync(
        HttpClient client,
        string query,
        object? variables = null)
    {
        var response = await client.PostAsJsonAsync("/graphql", new { query, variables });

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// For tests whose subject is the transport response itself rather than the GraphQL payload -
    /// TASK-042's 401/403, where the status code is the assertion and the body is only corroborating
    /// detail. Neither helper above surfaces the <see cref="HttpResponseMessage"/>, and the caller
    /// owns disposing what this returns.
    /// </summary>
    public static Task<HttpResponseMessage> PostRawAsync(
        HttpClient client,
        string query,
        object? variables = null) =>
        client.PostAsJsonAsync("/graphql", new { query, variables });
}
