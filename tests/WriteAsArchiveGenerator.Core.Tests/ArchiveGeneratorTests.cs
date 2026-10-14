using System.Net;
using WriteAs.NET;
using WriteAs.NET.Client.Models;
using WriteAs.NET.Shared;
using WriteAsArchiveGenerator.Core;

namespace WriteAsArchiveGenerator.Core.Tests;

public class ArchiveGeneratorTests
{
    /// <summary>Serves posts 10 per page, returning an empty page for the first N calls to each page listed in failures.</summary>
    private sealed class FakeClient(List<Post> posts, Dictionary<int, int>? failures = null) : IWriteAsClient
    {
        public List<int> PageCalls { get; } = [];
        public bool GetAllPostsCalled { get; private set; }

        public Task<List<Post>> GetPostsByPageNumber(string alias, int pageNumber, SortOrder sortOrder = SortOrder.Descending, bool saveToCache = false)
        {
            PageCalls.Add(pageNumber);
            if (failures is not null && failures.TryGetValue(pageNumber, out int remaining) && remaining > 0)
            {
                failures[pageNumber] = remaining - 1;
                return Task.FromResult(new List<Post>());
            }
            return Task.FromResult(posts.Skip((pageNumber - 1) * 10).Take(10).ToList());
        }

        public Task<List<Post>> GetAllPosts(string alias, SortOrder sortOrder = SortOrder.Descending)
        {
            GetAllPostsCalled = true;
            return Task.FromResult(posts);
        }

        public void Dispose() { }
        public Task<Post> GetPostById(string postId) => throw new NotImplementedException();
        public Task<Post> GetPostBySlug(string alias, string slug) => throw new NotImplementedException();
        public Task<List<Post>> Search(string alias, string searchKey, SortOrder sortOrder = SortOrder.Descending) => throw new NotImplementedException();
    }

    private sealed class CollectionHandler(HttpStatusCode status, int totalPosts) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent($$$"""{"code":200,"data":{"total_posts":{{{totalPosts}}}}}"""),
            });
    }

    private static List<Post> MakePosts(int count) => Enumerable.Range(1, count)
        .Select(i => new Post
        {
            Id = $"id{i}",
            Slug = $"post-{i}",
            Title = $"Post {i}",
            Body = "Body",
            CreateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-i),
        })
        .ToList();

    private static ArchiveGeneratorOptions Options(int totalPosts, int retries = 2, HttpStatusCode status = HttpStatusCode.OK) => new()
    {
        DelayBetweenPages = TimeSpan.Zero,
        RetryDelays = Enumerable.Repeat(TimeSpan.Zero, retries).ToList(),
        HttpClient = new HttpClient(new CollectionHandler(status, totalPosts)),
    };

    [Fact]
    public async Task FetchesEveryPage()
    {
        var client = new FakeClient(MakePosts(25));

        ArchiveResult result = await new ArchiveGenerator(client, Options(25)).GetItemsAsync("me");

        Assert.Equal([1, 2, 3], client.PageCalls);
        Assert.Equal(25, result.Items.Count);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task RetriesPagesThatComeBackEmpty()
    {
        var client = new FakeClient(MakePosts(25), new Dictionary<int, int> { [2] = 2 });

        ArchiveResult result = await new ArchiveGenerator(client, Options(25, retries: 2)).GetItemsAsync("me");

        Assert.Equal([1, 2, 2, 2, 3], client.PageCalls);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task ReportsIncompleteWhenRetriesRunOut()
    {
        var client = new FakeClient(MakePosts(25), new Dictionary<int, int> { [2] = 5 });

        ArchiveResult result = await new ArchiveGenerator(client, Options(25, retries: 2)).GetItemsAsync("me");

        Assert.Equal(15, result.Items.Count);
        Assert.Equal(25, result.ExpectedTotal);
        Assert.False(result.IsComplete);
    }

    [Fact]
    public async Task FallsBackToGetAllPostsWhenPostCountIsUnavailable()
    {
        var client = new FakeClient(MakePosts(3));

        ArchiveResult result = await new ArchiveGenerator(client, Options(0, status: HttpStatusCode.TooManyRequests)).GetItemsAsync("me");

        Assert.True(client.GetAllPostsCalled);
        Assert.Equal(3, result.Items.Count);
        Assert.Null(result.ExpectedTotal);
    }

    [Fact]
    public async Task BuildsWriteAsUrlsAndTitles()
    {
        List<Post> posts = MakePosts(2);
        posts[1].Title = "";
        posts[1].Body = "A short note";

        ArchiveResult result = await new ArchiveGenerator(new FakeClient(posts), Options(2)).GetItemsAsync("me");

        Assert.Equal("https://write.as/me/post-1", result.Items[0].Url);
        Assert.Equal("Post 1", result.Items[0].Title);
        Assert.Equal("A short note", result.Items[1].Title);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://example.com/")]
    public async Task UsesCustomBlogUrlWhenGiven(string blogUrl)
    {
        ArchiveResult result = await new ArchiveGenerator(new FakeClient(MakePosts(1)), Options(1)).GetItemsAsync("me", blogUrl);

        Assert.Equal("https://example.com/post-1", result.Items[0].Url);
    }
}
