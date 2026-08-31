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
}
