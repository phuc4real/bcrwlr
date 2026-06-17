namespace Bcrwlr.Api.Services;

/// <summary>Resolves on-disk locations for the archive (DB file + per-article folders).</summary>
public sealed class ArchivePaths
{
    public string DataDir { get; }
    public string ArticlesDir { get; }
    public string DbPath { get; }

    public ArchivePaths(IConfiguration config, IHostEnvironment env)
    {
        // Default to a `data/` folder at the solution root (project is at src/Bcrwlr.Api, so two levels
        // up), keeping the archive out of the project tree and clear of the source `Data/` folder
        // (Windows is case-insensitive, so a project-local `data` would collide with `Data`).
        var configured = config["Archive:DataDir"];
        DataDir = string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "data"))
            : Path.GetFullPath(configured);

        ArticlesDir = Path.Combine(DataDir, "articles");
        DbPath = Path.Combine(DataDir, "bcrwlr.db");

        Directory.CreateDirectory(ArticlesDir);
    }

    public string FolderFor(string id) => Path.Combine(ArticlesDir, id);
    public string HtmlPath(string id) => Path.Combine(FolderFor(id), "article.html");
    public string MarkdownPath(string id) => Path.Combine(FolderFor(id), "article.md");
    public string ImagesDir(string id) => Path.Combine(FolderFor(id), "images");
    public string ThumbPath(string id, string fileName) => Path.Combine(FolderFor(id), fileName);
}
