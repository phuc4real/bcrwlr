using Bcrwlr.Api.Data;
using Bcrwlr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bcrwlr.Api.Services;

/// <summary>
/// Persistence layer: writes article files to disk and the metadata row to SQLite,
/// and reads/deletes them. The DB is the index; the folder holds the content.
/// </summary>
public sealed class ArticleStore(AppDbContext db, ArchivePaths paths)
{
    public async Task<Article> SaveAsync(
        Article meta, string html, string markdown,
        IReadOnlyList<SavedImage> images, DownloadedImage? thumb, CancellationToken ct)
    {
        var folder = paths.FolderFor(meta.Id);
        Directory.CreateDirectory(folder);

        await File.WriteAllTextAsync(paths.HtmlPath(meta.Id), html, ct);
        await File.WriteAllTextAsync(paths.MarkdownPath(meta.Id), markdown, ct);

        if (images.Count > 0)
        {
            Directory.CreateDirectory(paths.ImagesDir(meta.Id));
            foreach (var img in images)
                await File.WriteAllBytesAsync(Path.Combine(paths.ImagesDir(meta.Id), img.FileName), img.Bytes, ct);
        }

        if (thumb is not null)
        {
            meta.ThumbFile = $"thumb{thumb.Extension}";
            await File.WriteAllBytesAsync(paths.ThumbPath(meta.Id, meta.ThumbFile), thumb.Bytes, ct);
        }

        db.Articles.Add(meta);
        await db.SaveChangesAsync(ct);
        return meta;
    }

    public async Task<List<ArticleSummary>> ListAsync(string? query, CancellationToken ct)
    {
        var q = db.Articles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(a =>
                EF.Functions.Like(a.Title, $"%{term}%") ||
                (a.Excerpt != null && EF.Functions.Like(a.Excerpt, $"%{term}%")) ||
                (a.SiteName != null && EF.Functions.Like(a.SiteName, $"%{term}%")));
        }

        var rows = await q.OrderByDescending(a => a.SavedAt).ToListAsync(ct);
        return rows.Select(ArticleSummary.From).ToList();
    }

    public Task<Article?> FindAsync(string id, CancellationToken ct)
        => db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<ArticleDetail?> GetDetailAsync(string id, CancellationToken ct)
    {
        var meta = await FindAsync(id, ct);
        if (meta is null) return null;

        var htmlPath = paths.HtmlPath(id);
        if (!File.Exists(htmlPath)) return null;

        var html = await File.ReadAllTextAsync(htmlPath, ct);
        return new ArticleDetail(ArticleSummary.From(meta), html);
    }

    public string? FilePath(Article meta, string format) => format switch
    {
        "html" => paths.HtmlPath(meta.Id),
        "md" or "markdown" => paths.MarkdownPath(meta.Id),
        _ => null
    };

    public string? ThumbPath(Article meta)
        => meta.ThumbFile is null ? null : paths.ThumbPath(meta.Id, meta.ThumbFile);

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        var meta = await db.Articles.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (meta is null) return false;

        var folder = paths.FolderFor(id);
        await TryDeleteDirectoryAsync(folder, ct);

        db.Articles.Remove(meta);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Deletes a folder, retrying briefly. On Windows a just-served file can hold a transient
    /// lock, so a single <c>Directory.Delete</c> may throw <see cref="IOException"/>.
    /// </summary>
    private static async Task TryDeleteDirectoryAsync(string folder, CancellationToken ct)
    {
        if (!Directory.Exists(folder)) return;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                Directory.Delete(folder, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(100 * attempt, ct);
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                await Task.Delay(100 * attempt, ct);
            }
        }
    }
}
