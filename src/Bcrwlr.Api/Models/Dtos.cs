namespace Bcrwlr.Api.Models;

/// <summary>Request body for saving a new article.</summary>
public record SaveRequest(string Url);

/// <summary>Card/list view of a saved article (no body content).</summary>
public record ArticleSummary(
    string Id,
    string Title,
    string? Author,
    string SourceUrl,
    string? SiteName,
    string? Excerpt,
    int WordCount,
    int ReadingMinutes,
    bool HasThumb,
    DateTime? PublishedAt,
    DateTime SavedAt)
{
    public static ArticleSummary From(Article a) => new(
        a.Id, a.Title, a.Author, a.SourceUrl, a.SiteName, a.Excerpt,
        a.WordCount, a.ReadingMinutes, a.ThumbFile is not null, a.PublishedAt, a.SavedAt);
}

/// <summary>Full reader view: summary metadata plus the self-contained article HTML.</summary>
public record ArticleDetail(ArticleSummary Summary, string Html);
