using System.Net;
using SmartReader;

namespace Bcrwlr.Api.Services;

/// <summary>
/// Wraps extracted content in a clean, self-contained reading-mode HTML document:
/// constrained line width, readable typography, source header, and light/dark support.
/// </summary>
public static class HtmlRenderer
{
    public static string Render(Article article, Uri sourceUri, string inlinedContentHtml)
    {
        var title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(article.Title) ? "Untitled" : article.Title);
        var byline = BuildByline(article, sourceUri);

        return $$"""
        <!DOCTYPE html>
        <html lang="{{WebUtility.HtmlEncode(article.Language ?? "en")}}">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{title}}</title>
          <style>
            :root { color-scheme: light dark; }
            * { box-sizing: border-box; }
            body {
              margin: 0;
              background: #fbfbf9;
              color: #1a1a1a;
              font-family: Georgia, 'Times New Roman', serif;
              line-height: 1.7;
              font-size: 19px;
            }
            main { max-width: 720px; margin: 0 auto; padding: 48px 24px 96px; }
            header.article-head { margin-bottom: 2rem; border-bottom: 1px solid rgba(0,0,0,.12); padding-bottom: 1.25rem; }
            h1 { font-size: 2.1rem; line-height: 1.25; margin: 0 0 .6rem; }
            .meta { font-family: -apple-system, system-ui, sans-serif; font-size: .85rem; color: #6b6b6b; }
            .meta a { color: inherit; }
            article :is(h1,h2,h3,h4) { line-height: 1.3; margin-top: 2rem; }
            article img, article figure img { max-width: 100%; height: auto; border-radius: 6px; }
            article figure { margin: 1.5rem 0; }
            figcaption { font-family: -apple-system, system-ui, sans-serif; font-size: .8rem; color: #6b6b6b; text-align: center; }
            article a { color: #1558d6; }
            blockquote { margin: 1.5rem 0; padding-left: 1.2rem; border-left: 3px solid rgba(0,0,0,.18); color: #444; }
            pre { background: rgba(0,0,0,.05); padding: 1rem; border-radius: 8px; overflow: auto; font-size: .85rem; }
            code { font-family: ui-monospace, 'SFMono-Regular', Consolas, monospace; }
            pre code { background: none; }
            :not(pre) > code { background: rgba(0,0,0,.06); padding: .1em .35em; border-radius: 4px; font-size: .85em; }
            table { border-collapse: collapse; width: 100%; font-size: .9rem; }
            th, td { border: 1px solid rgba(0,0,0,.15); padding: .4rem .6rem; }
            hr { border: none; border-top: 1px solid rgba(0,0,0,.12); margin: 2rem 0; }
            @media (prefers-color-scheme: dark) {
              body { background: #1b1b1d; color: #e3e3e3; }
              header.article-head { border-color: rgba(255,255,255,.14); }
              .meta, figcaption { color: #9a9a9a; }
              article a { color: #6ea8fe; }
              blockquote { border-color: rgba(255,255,255,.2); color: #bdbdbd; }
              pre, :not(pre) > code { background: rgba(255,255,255,.08); }
              th, td, hr { border-color: rgba(255,255,255,.15); }
            }
          </style>
        </head>
        <body>
          <main>
            <header class="article-head">
              <h1>{{title}}</h1>
              <div class="meta">{{byline}}</div>
            </header>
            <article>
        {{inlinedContentHtml}}
            </article>
          </main>
        </body>
        </html>
        """;
    }

    private static string BuildByline(Article article, Uri sourceUri)
    {
        var parts = new List<string>();

        var author = article.Author ?? article.Byline;
        if (!string.IsNullOrWhiteSpace(author))
            parts.Add(WebUtility.HtmlEncode(author.Trim()));

        var site = string.IsNullOrWhiteSpace(article.SiteName) ? sourceUri.Host : article.SiteName;
        var href = WebUtility.HtmlEncode(sourceUri.ToString());
        parts.Add($"<a href=\"{href}\" target=\"_blank\" rel=\"noopener\">{WebUtility.HtmlEncode(site)}</a>");

        if (article.PublicationDate is { } date)
            parts.Add(WebUtility.HtmlEncode(date.ToString("MMMM d, yyyy")));

        if (article.TimeToRead.TotalMinutes >= 1)
            parts.Add($"{(int)Math.Ceiling(article.TimeToRead.TotalMinutes)} min read");

        return string.Join(" &middot; ", parts);
    }
}
