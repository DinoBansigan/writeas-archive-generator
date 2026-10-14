using System.Net;
using System.Text.Json;

namespace WriteAsArchiveGenerator.Core;

/// <summary>
/// WriteAs.NET returns an empty list for any failed request (including a rate-limited page
/// partway through a blog), so these direct calls are used to detect and explain failures.
/// </summary>
public static class WriteAsDiagnostics
{
    /// <summary>
    /// Gets the blog's total number of posts from the collection endpoint, or null if it
    /// could not be retrieved.
    /// </summary>
    public static async Task<int?> GetTotalPostsAsync(string alias, HttpClient? httpClient = null)
    {
        try
        {
            using var response = await SendAsync($"api/collections/{Uri.EscapeDataString(alias)}", httpClient);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            return json.RootElement.TryGetProperty("data", out JsonElement data)
                && data.TryGetProperty("total_posts", out JsonElement total)
                && total.TryGetInt32(out int value)
                ? value
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Explains why no posts were returned by probing the same endpoint WriteAs.NET uses.
    /// </summary>
    public static async Task<string> ExplainNoPostsAsync(string alias, HttpClient? httpClient = null)
    {
        HttpResponseMessage response;
        try
        {
            response = await SendAsync($"api/collections/{Uri.EscapeDataString(alias)}/posts?page=1", httpClient);
        }
        catch (HttpRequestException ex)
        {
            return $"Could not reach Write.as: {ex.Message}";
        }

        using (response)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.OK => $"The blog '{alias}' exists but has no public posts.",
                HttpStatusCode.NotFound => $"No Write.as blog with the alias '{alias}' was found. Check the alias (the part after write.as/ in your blog's address).",
                HttpStatusCode.Gone => $"The blog '{alias}' has been deleted or suspended.",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"The blog '{alias}' is private or password-protected.",
                HttpStatusCode.TooManyRequests => RateLimitedMessage,
                _ => $"Write.as returned {(int)response.StatusCode} {response.ReasonPhrase}.",
            };
        }
    }

    public const string RateLimitedMessage =
        "Write.as is rate limiting requests from this network (HTTP 429). Wait a while and try again, " +
        "or set the WRITEAS_API_KEY environment variable if you have a Write.as API key.";

    private static async Task<HttpResponseMessage> SendAsync(string relativeUrl, HttpClient? httpClient)
    {
        using var ownedClient = httpClient is null ? new HttpClient() : null;
        HttpClient client = httpClient ?? ownedClient!;

        using var request = new HttpRequestMessage(HttpMethod.Get, ArchiveGenerator.DefaultWriteAsUrl + relativeUrl);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("WriteAsArchiveGenerator/1.0");

        HttpResponseMessage response = await client.SendAsync(request);
        // Buffer the body so it outlives the owned client.
        await response.Content.LoadIntoBufferAsync();
        return response;
    }
}
