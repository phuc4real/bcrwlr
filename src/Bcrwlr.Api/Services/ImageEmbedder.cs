using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Bcrwlr.Api.Services;

public sealed record SavedImage(string FileName, byte[] Bytes);

/// <summary>An image downloaded once, reusable as inline base64 or as an on-disk file.</summary>
public sealed record DownloadedImage(byte[] Bytes, string ContentType, string Extension);

/// <summary>
/// Result of processing an article's images:
/// <list type="bullet">
/// <item><see cref="InlinedHtml"/> — content with <c>data:</c> base64 image URIs (self-contained HTML).</item>
/// <item><see cref="LocalizedHtml"/> — content with <c>images/&lt;file&gt;</c> relative paths (for Markdown).</item>
/// <item><see cref="Images"/> — the image files to write next to the Markdown.</item>
/// </list>
/// </summary>
public sealed record EmbedResult(string InlinedHtml, string LocalizedHtml, IReadOnlyList<SavedImage> Images);

/// <summary>
/// Downloads every image referenced by the extracted content so saved articles survive the
/// source site going offline. Download failures are non-fatal: the original remote URL is kept.
/// </summary>
public sealed class ImageEmbedder(IHttpClientFactory httpFactory, ILogger<ImageEmbedder> logger)
{
    private const long MaxImageBytes = 20 * 1024 * 1024; // 20 MB per image
    private static readonly HtmlParser Parser = new();

    public async Task<EmbedResult> EmbedAsync(string contentHtml, Uri baseUri, CancellationToken ct)
    {
        var doc = Parser.ParseDocument($"<body>{contentHtml}</body>");
        var imgs = doc.QuerySelectorAll("img").OfType<IElement>().ToList();
        var client = httpFactory.CreateClient("fetch");

        var saved = new List<SavedImage>();
        var resolved = new List<(IElement Img, string File, string ContentType, byte[] Bytes)>();
        var index = 0;

        foreach (var img in imgs)
        {
            var raw = FirstNonEmpty(
                img.GetAttribute("src"), img.GetAttribute("data-src"), img.GetAttribute("data-original"));

            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Uri.TryCreate(baseUri, raw, out var abs))
                continue;

            var dl = await TryDownloadAsync(client, abs, ct);
            if (dl is null)
                continue;

            var file = $"image-{++index:D3}{dl.Extension}";
            resolved.Add((img, file, dl.ContentType, dl.Bytes));
            saved.Add(new SavedImage(file, dl.Bytes));
        }

        // Localized form (relative paths) — also drop srcset so browsers don't refetch remote variants.
        foreach (var (img, file, _, _) in resolved)
        {
            img.SetAttribute("src", $"images/{file}");
            img.RemoveAttribute("srcset");
        }
        var localized = doc.Body!.InnerHtml;

        // Inlined form (base64 data URIs) — fully self-contained.
        foreach (var (img, _, contentType, bytes) in resolved)
            img.SetAttribute("src", $"data:{contentType};base64,{Convert.ToBase64String(bytes)}");
        var inlined = doc.Body!.InnerHtml;

        return new EmbedResult(inlined, localized, saved);
    }

    /// <summary>Downloads a single image (used for the library thumbnail). Returns null on failure.</summary>
    public async Task<DownloadedImage?> TryDownloadAsync(Uri uri, CancellationToken ct)
        => await TryDownloadAsync(httpFactory.CreateClient("fetch"), uri, ct);

    private async Task<DownloadedImage?> TryDownloadAsync(HttpClient client, Uri uri, CancellationToken ct)
    {
        try
        {
            using var resp = await client.GetAsync(uri, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return null;
            if (resp.Content.Headers.ContentLength is > MaxImageBytes)
                return null;

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0 || bytes.Length > MaxImageBytes)
                return null;

            return new DownloadedImage(bytes, contentType, ExtensionFor(contentType, uri));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Skipping image {Uri}", uri);
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string ExtensionFor(string contentType, Uri uri)
    {
        var fromType = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            "image/avif" => ".avif",
            "image/bmp" => ".bmp",
            "image/x-icon" or "image/vnd.microsoft.icon" => ".ico",
            _ => null
        };
        if (fromType is not null)
            return fromType;

        var ext = Path.GetExtension(uri.AbsolutePath);
        return string.IsNullOrWhiteSpace(ext) || ext.Length > 5 ? ".img" : ext.ToLowerInvariant();
    }
}
