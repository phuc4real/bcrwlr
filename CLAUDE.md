# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

bcrwlr archives blog posts/articles offline: paste a URL, the backend fetches it, extracts a clean
reading-mode version, **embeds every image**, and saves it as both self-contained HTML and Markdown.
Backend is an ASP.NET Core Web API (.NET 10); frontend is a React + Vite + TS SPA served by the API.

## Commands

Backend (`src/Bcrwlr.Api`):
```bash
dotnet build bcrwlr.slnx          # build whole solution (run from repo root)
dotnet run                        # run API on http://localhost:5220 (from src/Bcrwlr.Api)
dotnet run --Archive:DataDir=C:\path   # override where the archive is stored
```

Frontend (`src/clientapp`):
```bash
npm install
npm run dev      # Vite dev server on :5173, proxies /api -> :5220
npm run build    # emits the SPA into ../Bcrwlr.Api/wwwroot (what the API serves in prod)
npm run lint     # eslint
```

**Dev loop:** run `dotnet run` and `npm run dev` in parallel, open http://localhost:5173.
**Prod-style:** `npm run build` then `dotnet run`, open http://localhost:5220 (single server).

There is no test project yet.

Docker (single image: SPA + API):
```bash
docker compose up -d --build     # http://localhost:8080, archive in the bcrwlr-data volume
docker build -t bcrwlr:latest .  # build only
```

## Architecture

The save pipeline is the core. `POST /api/articles { url }` flows through `ArchiveService.ArchiveAsync`
(`src/Bcrwlr.Api/Services/`), which composes the other services in order:

1. **`ArticleExtractor`** — fetches the page via the named `HttpClient` `"fetch"` and runs **SmartReader**
   (`new Reader(uri, html).GetArticle()`). SmartReader is the .NET Mozilla-Readability port and already
   absolutizes relative asset URLs. Throws `ExtractionException` (→ HTTP 422) on fetch/parse failure.
2. **`ImageEmbedder`** — downloads each image **once** and returns an `EmbedResult` with two renderings of
   the same content: `InlinedHtml` (base64 `data:` URIs, self-contained) and `LocalizedHtml`
   (`images/<file>` relative paths). Failed downloads keep the original remote URL (non-fatal).
3. **`HtmlRenderer`** (static) wraps `InlinedHtml` in the reading-mode template; **`MarkdownRenderer`**
   (static, **ReverseMarkdown**) converts `LocalizedHtml` and prepends YAML frontmatter.
4. **`ArticleStore`** writes files to disk and the metadata row to SQLite.

**Two-store split — important:** SQLite (EF Core, `AppDbContext`) is only the **metadata index** for
list/search. The actual content lives on disk under `data/articles/{id}/` (`article.html`, `article.md`,
`images/`, `thumb.*`). `ArchivePaths` owns all path resolution and is the single source of truth for
locations; `ArticleStore` reads/writes through it. Both the DB row and the folder must stay in sync —
`DeleteAsync` removes both.

The in-app reader renders the saved self-contained HTML via an `<iframe srcDoc>`; because images are
inlined as base64, no extra asset endpoints are needed for reading. The library card thumbnail is the one
asset served separately, via `GET /api/articles/{id}/thumb`.

`Program.cs` instantiates `ArchivePaths` **before** registering the DbContext (the SQLite path comes from
it), then sets up the `"fetch"` HttpClient (UA, redirects, decompression), DI, dev CORS, and the static
files + `MapFallbackToFile("index.html")` SPA fallback. `EnsureCreated()` builds the schema on startup —
there are no EF migrations, so model changes need the dev DB deleted (just delete `data/`).

## Gotchas

- **Case-insensitive filesystem:** the runtime archive dir is deliberately the **repo root** `data/`
  (computed two levels up from the project in `ArchivePaths`), NOT inside `src/Bcrwlr.Api/`, because a
  project-local `data/` would collide with the source `Data/` folder on Windows. Keep it that way.
- **`SmartReader.Article` vs `Models.Article`** name-clash: `ArchiveService` aliases the SmartReader type
  as `SaArticle`. The EF entity is `Bcrwlr.Api.Models.Article`.
- Deleting an article folder uses a short **retry loop** (`TryDeleteDirectoryAsync`) — a just-served file
  can briefly hold a Windows lock and make a single `Directory.Delete` throw.
- In the container the archive dir is set via `Archive__DataDir=/data` (the local-dev "two levels up"
  default is wrong inside `/app`); `/data` is a volume, so saved articles persist across restarts.

## Known limitations

Extraction works on **static HTML** only (JS-rendered pages extract poorly); paywalled pages can't be
fetched; re-saving the same URL creates a new entry.
