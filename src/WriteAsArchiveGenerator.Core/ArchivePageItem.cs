namespace WriteAsArchiveGenerator.Core;

/// <summary>
/// A single entry (one blog post) on the archive page.
/// </summary>
public sealed record ArchivePageItem(string Title, string Url, DateTime PublishDate);
