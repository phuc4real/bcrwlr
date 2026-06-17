using SmartReader;

namespace Bcrwlr.Api.Services;

/// <summary>Thrown when a page cannot be fetched or no readable article can be extracted.</summary>
public sealed class ExtractionException(string message) : Exception(message);

/// <summary>
/// The SmartReader result, the final (post-redirect) URI used as the asset base, and the
/// post-processed content HTML (code blocks restored). Use <see cref="ContentHtml"/> rather than
/// <c>Article.Content</c> downstream.
/// </summary>
public sealed record ExtractedArticle(Article Article, Uri BaseUri, string ContentHtml, string? LeadImage);

/// <summary>
/// Fetches a URL and runs Mozilla-Readability extraction (via SmartReader) to obtain
/// clean reading-mode content. SmartReader absolutizes relative asset URLs against the base.
/// </summary>
public sealed class ArticleExtractor(IHttpClientFactory httpFactory, ILogger<ArticleExtractor> logger)
{
    public async Task<ExtractedArticle> ExtractAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ExtractionException("Please provide a valid http(s) URL.");
        }

        var client = httpFactory.CreateClient("fetch");

        HttpResponseMessage resp;
        try
        {
            resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch {Url}", uri);
            throw new ExtractionException("Could not reach the page. Check the URL and your connection.");
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
                throw new ExtractionException($"The page returned {(int)resp.StatusCode} {resp.ReasonPhrase}.");

            var finalUri = resp.RequestMessage?.RequestUri ?? uri;
            var html = await resp.Content.ReadAsStringAsync(ct);

            // Protect code blocks (readability drops them) behind placeholders before extraction.
            var pre = HtmlPreprocessor.Normalize(html);

            Article article;
            try
            {
                article = new Reader(finalUri.ToString(), pre.Html).GetArticle();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SmartReader failed on {Url}", finalUri);
                throw new ExtractionException("Could not parse readable content from this page.");
            }

            if (article is null || !article.IsReadable || string.IsNullOrWhiteSpace(article.Content))
                throw new ExtractionException("No readable article content was found on this page.");

            // Restore the real code blocks into the extracted content.
            var content = HtmlPreprocessor.RestoreCodeBlocks(article.Content, pre.CodeBlocks);

            return new ExtractedArticle(article, finalUri, content, pre.LeadImage);
        }
    }
}
