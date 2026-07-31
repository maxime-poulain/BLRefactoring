using System.Net.Http.Json;

namespace BLRefactoring.DDD.Api.IntegrationTests.Fixtures;

/// <summary>
/// Helpers for the conditional-request dance every edit now goes through: read the
/// resource, keep its <c>ETag</c>, send it back as <c>If-Match</c>.
/// </summary>
public static class ConditionalRequestHelper
{
    /// <summary>
    /// Reads a resource and returns the entity tag it was served with.
    /// </summary>
    public static async Task<string> GetETagAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return response.Headers.ETag?.ToString()
               ?? throw new InvalidOperationException($"GET {url} returned no ETag.");
    }

    /// <summary>
    /// Sends a PUT carrying the given <c>If-Match</c>.
    /// </summary>
    public static Task<HttpResponseMessage> PutWithIfMatchAsync<TBody>(
        this HttpClient client, string url, TBody body, string entityTag)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", entityTag);

        return client.SendAsync(request);
    }
}
