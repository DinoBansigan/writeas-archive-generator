using System.Net;
using WriteAsArchiveGenerator.Core;

namespace WriteAsArchiveGenerator.Core.Tests;

public class WriteAsDiagnosticsTests
{
    private sealed class StubHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        public Uri? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "No Write.as blog with the alias")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate limiting")]
    [InlineData(HttpStatusCode.OK, "has no public posts")]
    [InlineData(HttpStatusCode.Forbidden, "private")]
    public async Task ExplainsStatusCodeOfPostsEndpoint(HttpStatusCode status, string expected)
    {
        var handler = new StubHandler(status);

        string message = await WriteAsDiagnostics.ExplainNoPostsAsync("me", new HttpClient(handler));

        Assert.Contains(expected, message);
        Assert.Equal("https://write.as/api/collections/me/posts?page=1", handler.RequestedUri?.ToString());
    }

    [Fact]
    public async Task ReadsTotalPostsFromCollection()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"code":200,"data":{"alias":"me","total_posts":42}}""");

        int? total = await WriteAsDiagnostics.GetTotalPostsAsync("me", new HttpClient(handler));

        Assert.Equal(42, total);
        Assert.Equal("https://write.as/api/collections/me", handler.RequestedUri?.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, """{"data":{}}""")]
    public async Task TotalPostsIsNullWhenUnavailable(HttpStatusCode status, string body)
    {
        int? total = await WriteAsDiagnostics.GetTotalPostsAsync("me", new HttpClient(new StubHandler(status, body)));

        Assert.Null(total);
    }
}
