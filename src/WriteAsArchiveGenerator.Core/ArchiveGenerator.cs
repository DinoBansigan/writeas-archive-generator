using WriteAs.NET;
using WriteAs.NET.Client.Models;
using WriteAs.NET.Shared;

namespace WriteAsArchiveGenerator.Core;

/// <summary>
/// The posts fetched for an archive, plus how many the blog says it has so callers can
/// tell whether anything is missing.
/// </summary>
public sealed record ArchiveResult(IReadOnlyList<ArchivePageItem> Items, int? ExpectedTotal)
{
    public bool IsComplete => ExpectedTotal is null || Items.Count >= ExpectedTotal;
}

/// <summary>
/// Fetches all public posts for a Write.as blog and turns them into archive page items.
/// </summary>
/// <remarks>
/// Write.as rate limits the posts endpoint, and WriteAs.NET returns an empty page instead of
/// an error when that happens. So this pages through the blog itself, pausing between pages
/// and retrying any page that comes back empty.
/// </remarks>
public sealed class ArchiveGenerator(IWriteAsClient client, ArchiveGeneratorOptions? options = null)
{
    public const string DefaultWriteAsUrl = "https://write.as/";
    private const int PostsPerPage = 10; // Fixed by the Write.as API

    private readonly ArchiveGeneratorOptions _options = options ?? new ArchiveGeneratorOptions();

    /// <param name="alias">The blog's Write.as alias, e.g. "dinobansigan".</param>
    /// <param name="blogUrl">
    /// Base URL for post links. Defaults to https://write.as/{alias}/. Pass a custom
    /// domain (e.g. https://example.com/) to link there instead.
    /// </param>
    /// <param name="progress">Receives status messages while pages are fetched.</param>
    public async Task<ArchiveResult> GetItemsAsync(
        string alias, string? blogUrl = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        int? totalPosts = await WriteAsDiagnostics.GetTotalPostsAsync(alias, _options.HttpClient);
        List<Post> posts = totalPosts is int total
            ? await FetchAllPagesAsync(alias, total, progress, cancellationToken)
            : await client.GetAllPosts(alias, SortOrder.Descending); // Couldn't get a post count; fall back to the library.

        string baseUrl = EnsureTrailingSlash(blogUrl ?? $"{DefaultWriteAsUrl}{alias}");
        List<ArchivePageItem> items = posts
            .DistinctBy(p => p.Id ?? p.Slug)
            .OrderByDescending(p => p.CreateDate)
            .Select(p => new ArchivePageItem(
                PostTitleGenerator.GetTitle(p.Title, p.Body),
                $"{baseUrl}{p.Slug}",
                p.CreateDate))
            .ToList();

        return new ArchiveResult(items, totalPosts);
    }

    private async Task<List<Post>> FetchAllPagesAsync(string alias, int totalPosts, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        int totalPages = (int)Math.Ceiling(totalPosts / (double)PostsPerPage);
        var posts = new List<Post>(totalPosts);

        for (int page = 1; page <= totalPages; page++)
        {
            if (page > 1)
            {
                await Task.Delay(_options.DelayBetweenPages, cancellationToken);
            }

            progress?.Report($"Fetching page {page} of {totalPages}...");
            List<Post> pagePosts = await client.GetPostsByPageNumber(alias, page);

            for (int attempt = 0; pagePosts.Count == 0 && attempt < _options.RetryDelays.Count; attempt++)
            {
                TimeSpan delay = _options.RetryDelays[attempt];
                progress?.Report($"Page {page} came back empty (likely rate limited). Retrying in {delay.TotalSeconds:0}s...");
                await Task.Delay(delay, cancellationToken);
                pagePosts = await client.GetPostsByPageNumber(alias, page);
            }

            posts.AddRange(pagePosts);
        }

        return posts;
    }

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";
}

public sealed class ArchiveGeneratorOptions
{
    /// <summary>Pause between page requests, to stay under the Write.as rate limit.</summary>
    public TimeSpan DelayBetweenPages { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Waits before each retry of a page that came back empty.</summary>
    public IReadOnlyList<TimeSpan> RetryDelays { get; init; } =
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    /// <summary>Client used to read the blog's post count. A new one is created when null.</summary>
    public HttpClient? HttpClient { get; init; }
}
