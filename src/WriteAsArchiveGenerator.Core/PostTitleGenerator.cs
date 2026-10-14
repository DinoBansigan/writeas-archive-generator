using System.Text.RegularExpressions;
using Markdig;

namespace WriteAsArchiveGenerator.Core;

/// <summary>
/// Produces the display title for a post. Untitled posts get a title built from
/// the first few words of their body.
/// </summary>
public static partial class PostTitleGenerator
{
    public const string UntitledFallback = "Untitled Post";
    public const int MaxWordsFromBody = 8;

    public static string GetTitle(string? title, string? markdownBody)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        if (string.IsNullOrWhiteSpace(markdownBody))
        {
            return UntitledFallback;
        }

        string plainText = Markdown.ToPlainText(markdownBody).Trim();
        if (plainText.Length == 0)
        {
            return UntitledFallback;
        }

        string[] words = Whitespace().Split(plainText);
        if (words.Length <= MaxWordsFromBody)
        {
            return string.Join(' ', words);
        }

        return string.Join(' ', words.Take(MaxWordsFromBody)) + "...";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
