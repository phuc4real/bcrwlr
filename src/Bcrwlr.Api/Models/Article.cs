namespace Bcrwlr.Api.Models;

/// <summary>
/// Metadata index row for a saved article. The actual content (HTML/Markdown/images)
/// lives on disk under <c>data/articles/{Id}/</c>; this row powers list/search.
/// </summary>
public class Article
{
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Author { get; set; }
    public string SourceUrl { get; set; } = default!;
    public string? SiteName { get; set; }
    public string? Excerpt { get; set; }
    public int WordCount { get; set; }
    public int ReadingMinutes { get; set; }

    /// <summary>File name of the saved thumbnail inside the article folder, or null.</summary>
    public string? ThumbFile { get; set; }

    public DateTime? PublishedAt { get; set; }
    public DateTime SavedAt { get; set; }
}
