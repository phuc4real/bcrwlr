using Bcrwlr.Api.Models;
using SaArticle = SmartReader.Article;

namespace Bcrwlr.Api.Services;

/// <summary>
/// Orchestrates the save pipeline: extract → embed images → render HTML + Markdown →
/// resolve thumbnail → persist. Each saved article is fully self-contained.
/// </summary>
public sealed class ArchiveService(
    ArticleExtractor extractor,
    ImageEmbedder embedder,
    ArticleStore store,
    ILogger<ArchiveService> logger)
{
    public async Task<ArticleSummary> ArchiveAsync(string url, CancellationToken ct)
    {
        var (article, baseUri) = await extractor.ExtractAsync(url, ct);

        var embed = await embedder.EmbedAsync(article.Content, baseUri, ct);

        var savedAt = DateTime.UtcNow;
        var html = HtmlRenderer.Render(article, baseUri, embed.InlinedHtml);
        var markdown = MarkdownRenderer.Render(article, baseUri, embed.LocalizedHtml, savedAt);
        var thumb = await ResolveThumbAsync(article, baseUri, embed, ct);

        var meta = new Article
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Title = string.IsNullOrWhiteSpace(article.Title) ? "Untitled" : article.Title.Trim(),
            Author = (article.Author ?? article.Byline)?.Trim(),
            SourceUrl = baseUri.ToString(),
            SiteName = string.IsNullOrWhiteSpace(article.SiteName) ? baseUri.Host : article.SiteName,
            Excerpt = article.Excerpt?.Trim(),
            WordCount = WordCount(article),
            ReadingMinutes = (int)Math.Ceiling(article.TimeToRead.TotalMinutes),
            PublishedAt = article.PublicationDate,
            SavedAt = savedAt,
        };

        await store.SaveAsync(meta, html, markdown, embed.Images, thumb, ct);
        logger.LogInformation("Archived \"{Title}\" ({Id}) from {Url}", meta.Title, meta.Id, baseUri);
        return ArticleSummary.From(meta);
    }

    private async Task<DownloadedImage?> ResolveThumbAsync(
        SaArticle article, Uri baseUri, EmbedResult embed, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(article.FeaturedImage)
            && Uri.TryCreate(baseUri, article.FeaturedImage, out var featured))
        {
            var dl = await embedder.TryDownloadAsync(featured, ct);
            if (dl is not null) return dl;
        }

        var first = embed.Images.FirstOrDefault();
        return first is null
            ? null
            : new DownloadedImage(first.Bytes, "", Path.GetExtension(first.FileName));
    }

    private static int WordCount(SaArticle a)
    {
        if (!string.IsNullOrWhiteSpace(a.TextContent))
            return a.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return a.Length > 0 ? a.Length / 5 : 0;
    }
}
