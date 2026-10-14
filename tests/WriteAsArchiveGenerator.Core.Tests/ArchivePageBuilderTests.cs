using WriteAsArchiveGenerator.Core;

namespace WriteAsArchiveGenerator.Core.Tests;

public class ArchivePageBuilderTests
{
    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GroupsPostsByYearNewestFirst()
    {
        var items = new[]
        {
            new ArchivePageItem("Old", "https://write.as/me/old", Utc(2023, 5, 1)),
            new ArchivePageItem("New", "https://write.as/me/new", Utc(2025, 1, 2)),
            new ArchivePageItem("Newer", "https://write.as/me/newer", Utc(2025, 3, 4)),
        };

        string html = ArchivePageBuilder.Build(items, TimeZoneInfo.Utc);

        string expected =
            "<div class=\"archivePage\">\n" +
            "<h1>2025</h1>\n" +
            "<hr class=\"archivePageHr\"/>\n" +
            "<div><a href=\"https://write.as/me/newer\" target=\"_blank\"><span class=\"archivePageDateSpan\">2025-03-04:</span> Newer</a></div>\n" +
            "<div><a href=\"https://write.as/me/new\" target=\"_blank\"><span class=\"archivePageDateSpan\">2025-01-02:</span> New</a></div>\n" +
            "<h1>2023</h1>\n" +
            "<hr class=\"archivePageHr\"/>\n" +
            "<div><a href=\"https://write.as/me/old\" target=\"_blank\"><span class=\"archivePageDateSpan\">2023-05-01:</span> Old</a></div>\n" +
            "</div>\n";

        Assert.Equal(expected, html.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void EncodesHtmlAndHashesInTitles()
    {
        var items = new[] { new ArchivePageItem("C# & <Blazor>", "https://write.as/me/x", Utc(2025, 1, 1)) };

        string html = ArchivePageBuilder.Build(items, TimeZoneInfo.Utc);

        Assert.Contains("C&#35; &amp; &lt;Blazor&gt;</a>", html);
    }

    [Theory]
    [InlineData("Game Log | PES2021 – 005 🎮⚽", "Game Log | PES2021 – 005 🎮⚽")]
    [InlineData("Game Log | Starfield – 006 🎮🚀", "Game Log | Starfield – 006 🎮🚀")]
    [InlineData("C# 🎮 & \"more\"", "C&#35; 🎮 &amp; &quot;more&quot;")]
    public void LeavesEmojiUnencoded(string title, string expected)
    {
        var items = new[] { new ArchivePageItem(title, "https://write.as/me/x", Utc(2025, 1, 1)) };

        string html = ArchivePageBuilder.Build(items, TimeZoneInfo.Utc);

        Assert.Contains($"</span> {expected}</a>", html);
        Assert.DoesNotContain("&&#35;", html);
    }

    [Fact]
    public void ConvertsDatesToRequestedTimeZone()
    {
        // 03:00 UTC on Jan 1 is still Dec 31 in US Pacific time.
        var items = new[] { new ArchivePageItem("T", "u", new DateTime(2025, 1, 1, 3, 0, 0, DateTimeKind.Utc)) };
        TimeZoneInfo pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

        string html = ArchivePageBuilder.Build(items, pacific);

        Assert.Contains("<h1>2024</h1>", html);
        Assert.Contains("2024-12-31:", html);
    }
}
