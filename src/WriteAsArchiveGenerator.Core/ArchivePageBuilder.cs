using System.Text;

namespace WriteAsArchiveGenerator.Core;

/// <summary>
/// Builds the HTML that gets pasted into a Write.as page. Posts are grouped by year,
/// and the CSS classes (archivePage, archivePageHr, archivePageDateSpan) match the
/// output of the original apps.dinobansigan.com generator so existing blog CSS keeps working.
/// </summary>
public static class ArchivePageBuilder
{
    public static string Build(IEnumerable<ArchivePageItem> items, TimeZoneInfo? timeZone = null)
    {
        timeZone ??= TimeZoneInfo.Local;

        var html = new StringBuilder();
        html.AppendLine("<div class=\"archivePage\">");

        int? currentYear = null;
        foreach (ArchivePageItem item in items.OrderByDescending(i => i.PublishDate))
        {
            DateTime publishDate = ToTimeZone(item.PublishDate, timeZone);
            if (publishDate.Year != currentYear)
            {
                currentYear = publishDate.Year;
                html.AppendLine($"<h1>{currentYear}</h1>");
                html.AppendLine("<hr class=\"archivePageHr\"/>");
            }

            html.AppendLine(
                $"<div><a href=\"{HtmlEscape(item.Url)}\" target=\"_blank\">" +
                $"<span class=\"archivePageDateSpan\">{publishDate:yyyy-MM-dd}:</span> {EncodeTitle(item.Title)}</a></div>");
        }

        html.AppendLine("</div>");
        return html.ToString();
    }

    private static DateTime ToTimeZone(DateTime value, TimeZoneInfo timeZone)
    {
        DateTime utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc), // Write.as timestamps are UTC
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
    }

    // Escapes only the characters HTML requires. WebUtility.HtmlEncode isn't used because it
    // turns emoji like 🎮 into numeric entities (&#127918;), which the '#' replacement below would break.
    // '&' must be escaped first so it doesn't re-escape the entities added after it.
    private static string HtmlEscape(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    // Encoding '#' stops Write.as from turning words in titles into hashtag links.
    private static string EncodeTitle(string title) => HtmlEscape(title).Replace("#", "&#35;");
}
