using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace Bcrwlr.Api.Services;

/// <summary>
/// The normalized HTML, the code blocks swapped out for placeholders, and a best-effort lead/cover
/// image URL (used as a fallback when readability and metadata don't surface one).
/// </summary>
public sealed record PreprocessResult(string Html, IReadOnlyList<string> CodeBlocks, string? LeadImage);

/// <summary>
/// Normalizes source HTML <b>before</b> readability extraction and restores protected content
/// afterwards. Readability discards <c>&lt;pre&gt;</c> code blocks on many sites (Medium/Shiki/Gist
/// etc.) even when well-formed, so we replace each block with a plain-text placeholder — which
/// readability always keeps — then swap the real (fenced-ready) code back into the extracted content.
/// </summary>
public static class HtmlPreprocessor
{
    private static readonly HtmlParser Parser = new();
    private const string Token = "BCRWLRCODEBLOCK";

    private static readonly string[] ChromeImageHints =
        ["avatar", "rounded-full", "icon", "logo", "badge", "emoji", "spinner"];

    public static PreprocessResult Normalize(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new PreprocessResult(html, [], null);

        var doc = Parser.ParseDocument(html);

        // Best-effort lead image: the first non-chrome <img> in document order. Captured before any
        // cleaning so we can re-add the cover if readability/metadata drop it.
        string? leadImage = null;
        foreach (var img in doc.QuerySelectorAll("img"))
        {
            var src = img.GetAttribute("src") ?? img.GetAttribute("data-src");
            if (string.IsNullOrWhiteSpace(src) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            var cls = (img.GetAttribute("class") ?? "").ToLowerInvariant();
            if (ChromeImageHints.Any(cls.Contains))
                continue;
            if (int.TryParse(img.GetAttribute("width"), out var w) && w is > 0 and < 200)
                continue;

            leadImage = src;
            break;
        }

        // Drop elements hidden via inline style, the `hidden` attribute, or the Tailwind `.hidden`
        // utility (display:none) — usually duplicate light/dark renderings of the same code block.
        foreach (var el in doc
                     .QuerySelectorAll("[hidden], [style*='display:none'], [style*='display: none'], .hidden")
                     .ToList())
        {
            el.Remove();
        }

        var codeBlocks = new List<string>();
        foreach (var pre in doc.QuerySelectorAll("pre").ToList())
        {
            // Highlighters render each source line as a `.line` span; join those to keep newlines.
            var lines = pre.QuerySelectorAll(".line").ToList();
            var code = lines.Count > 0
                ? string.Join("\n", lines.Select(l => l.TextContent))
                : pre.TextContent;

            if (string.IsNullOrWhiteSpace(code))
                continue;

            // Swap the block for a placeholder paragraph that readability will retain in place.
            var marker = $"{Token}{codeBlocks.Count}END";
            codeBlocks.Add(code.TrimEnd());

            var placeholder = doc.CreateElement("p");
            placeholder.TextContent = marker;
            pre.Replace(placeholder);
        }

        return new PreprocessResult(doc.DocumentElement.OuterHtml, codeBlocks, leadImage);
    }

    /// <summary>Replaces the placeholders in extracted content with real fenced-ready code blocks.</summary>
    public static string RestoreCodeBlocks(string contentHtml, IReadOnlyList<string> codeBlocks)
    {
        if (string.IsNullOrEmpty(contentHtml) || codeBlocks.Count == 0)
            return contentHtml;

        for (var i = 0; i < codeBlocks.Count; i++)
        {
            var marker = $"{Token}{i}END";
            var block = $"<pre><code>{WebUtility.HtmlEncode(codeBlocks[i])}</code></pre>";

            // The placeholder usually survives as `<p>MARKER</p>`; unwrap that so we don't nest
            // <pre> inside <p>. Fall back to a bare replacement if the wrapper changed.
            // Use a MatchEvaluator so `$` in the code (e.g. C# `$"..."`) isn't treated as a regex
            // substitution token; string.Replace is already literal.
            var wrapped = new Regex($@"<p[^>]*>\s*{marker}\s*</p>", RegexOptions.IgnoreCase);
            contentHtml = wrapped.IsMatch(contentHtml)
                ? wrapped.Replace(contentHtml, _ => block)
                : contentHtml.Replace(marker, block);
        }

        return contentHtml;
    }
}
