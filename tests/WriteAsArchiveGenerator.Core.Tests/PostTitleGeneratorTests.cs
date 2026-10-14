using WriteAsArchiveGenerator.Core;

namespace WriteAsArchiveGenerator.Core.Tests;

public class PostTitleGeneratorTests
{
    [Fact]
    public void UsesPostTitleWhenPresent()
    {
        Assert.Equal("My Post", PostTitleGenerator.GetTitle("  My Post ", "Body text"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FallsBackWhenTitleAndBodyAreEmpty(string? body)
    {
        Assert.Equal(PostTitleGenerator.UntitledFallback, PostTitleGenerator.GetTitle(null, body));
    }

    [Fact]
    public void ShortUntitledPostUsesWholeBodyWithoutEllipsis()
    {
        // The old generator threw IndexOutOfRangeException for bodies under 8 words.
        Assert.Equal("Just three words", PostTitleGenerator.GetTitle(null, "Just three words"));
    }

    [Fact]
    public void LongUntitledPostIsTruncatedToEightWords()
    {
        string body = "one two three four five six seven eight nine ten";
        Assert.Equal("one two three four five six seven eight...", PostTitleGenerator.GetTitle(null, body));
    }

    [Fact]
    public void StripsMarkdownAndKeepsWordsAcrossParagraphsSeparate()
    {
        string body = "Some **bold** and [a link](https://example.com).\n\nNext paragraph here";
        Assert.Equal("Some bold and a link. Next paragraph here", PostTitleGenerator.GetTitle(null, body));
    }
}
