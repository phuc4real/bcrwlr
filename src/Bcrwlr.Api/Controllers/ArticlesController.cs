using Bcrwlr.Api.Models;
using Bcrwlr.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Bcrwlr.Api.Controllers;

[ApiController]
[Route("api/articles")]
public class ArticlesController(
    ArchiveService archive,
    ArticleStore store,
    ILogger<ArticlesController> logger) : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>Fetch a URL, extract reading-mode content, embed images, and save it locally.</summary>
    [HttpPost]
    public async Task<ActionResult<ArticleSummary>> Save([FromBody] SaveRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "A URL is required." });

        try
        {
            var summary = await archive.ArchiveAsync(request.Url, ct);
            return CreatedAtAction(nameof(Get), new { id = summary.Id }, summary);
        }
        catch (ExtractionException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error archiving {Url}", request.Url);
            return StatusCode(500, new { error = "Something went wrong while saving this article." });
        }
    }

    /// <summary>List saved articles (newest first), optionally filtered by a search term.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ArticleSummary>>> List([FromQuery] string? q, CancellationToken ct)
        => Ok(await store.ListAsync(q, ct));

    /// <summary>Get one article's metadata plus its self-contained HTML for the in-app reader.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ArticleDetail>> Get(string id, CancellationToken ct)
    {
        var detail = await store.GetDetailAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Download the saved file in the requested format.</summary>
    [HttpGet("{id}/file")]
    public async Task<IActionResult> Download(string id, [FromQuery] string format, CancellationToken ct)
    {
        var meta = await store.FindAsync(id, ct);
        if (meta is null) return NotFound();

        var path = store.FilePath(meta, (format ?? "").ToLowerInvariant());
        if (path is null) return BadRequest(new { error = "format must be 'html' or 'md'." });
        if (!System.IO.File.Exists(path)) return NotFound();

        var ext = Path.GetExtension(path);
        var contentType = ext == ".md" ? "text/markdown" : "text/html";
        var fileName = $"{Slug(meta.Title)}{ext}";
        return PhysicalFile(path, contentType, fileName);
    }

    /// <summary>Serve the saved thumbnail image for library cards.</summary>
    [HttpGet("{id}/thumb")]
    public async Task<IActionResult> Thumb(string id, CancellationToken ct)
    {
        var meta = await store.FindAsync(id, ct);
        var path = meta is null ? null : store.ThumbPath(meta);
        if (path is null || !System.IO.File.Exists(path)) return NotFound();

        if (!ContentTypes.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";
        return PhysicalFile(path, contentType);
    }

    /// <summary>Delete a saved article and its on-disk folder.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
        => await store.DeleteAsync(id, ct) ? NoContent() : NotFound();

    private static string Slug(string title)
    {
        var chars = title.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Length > 60 ? slug[..60].Trim('-') : slug;
        return string.IsNullOrWhiteSpace(slug) ? "article" : slug;
    }
}
