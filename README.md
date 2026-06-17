# bcrwlr

A local web app to **archive blog posts and articles offline**. Paste a link and bcrwlr fetches the
page, strips it down to a clean **reading-mode** view (like Chrome/Firefox Reader View), **embeds all
images**, and saves it locally as both a **self-contained HTML** file and a **Markdown** file — so your
reads survive even after the original site goes down.

## How it works

- **Backend** — ASP.NET Core Web API (.NET 10)
  - [SmartReader](https://github.com/Strumenta/SmartReader) (the .NET port of Mozilla Readability) extracts the article content.
  - Every image is downloaded and **inlined as base64** in the HTML and **saved as files** beside the Markdown.
  - [ReverseMarkdown](https://github.com/mysticmind/reversemarkdown-net) produces the Markdown (with YAML frontmatter).
  - Metadata is indexed in **SQLite** (EF Core); content lives on disk under `data/articles/{id}/`.
- **Frontend** — React + Vite + TypeScript SPA (paste bar, searchable library, in-app reader).
  In production the SPA is built into the API's `wwwroot` and served by the API.

## Project layout

```
bcrwlr/
├── src/
│   ├── Bcrwlr.Api/        # ASP.NET Core Web API (+ wwwroot: built SPA)
│   └── clientapp/         # React + Vite + TS SPA
└── data/                  # runtime: bcrwlr.db + articles/{id}/{article.html, article.md, images/, thumb.*}
```

## Run it

### Development (two processes, hot reload)

```bash
# Terminal 1 — API on http://localhost:5220
cd src/Bcrwlr.Api
dotnet run

# Terminal 2 — Vite dev server on http://localhost:5173 (proxies /api to the backend)
cd src/clientapp
npm install
npm run dev
```

Open **http://localhost:5173**.

### Production-style (single server)

```bash
cd src/clientapp
npm run build            # emits the SPA into ../Bcrwlr.Api/wwwroot

cd ../Bcrwlr.Api
dotnet run               # serves both the SPA and the API
```

Open **http://localhost:5220**.

### Docker (single image — recommended for deploy)

The whole app (SPA + API) ships as one image. The archive is stored in a `/data` volume so it survives
container restarts and upgrades.

```bash
# with docker compose (named volume + restart policy)
docker compose up -d --build      # then open http://localhost:8080

# or plain docker
docker build -t bcrwlr:latest .
docker run -d --name bcrwlr -p 8080:8080 -v bcrwlr-data:/data bcrwlr:latest
```

Prebuilt images are published to **GitHub Container Registry** by CI (`.github/workflows/docker-publish.yml`)
on every push to `main` and every `v*.*.*` tag:

```bash
docker pull ghcr.io/<owner>/bcrwlr:latest
docker run -d -p 8080:8080 -v bcrwlr-data:/data ghcr.io/<owner>/bcrwlr:latest
```

- The container listens on **8080** (HTTP) as a non-root user; put it behind a TLS-terminating reverse
  proxy (nginx/Traefik/Caddy) in production.
- Liveness/readiness probe: **`GET /healthz`** (also wired as the image `HEALTHCHECK`).
- Override the archive location with the `Archive__DataDir` env var (defaults to `/data` in the image).
- API responses (multi-MB self-contained HTML) are Brotli/Gzip compressed.

## API

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/articles` | Body `{ "url": "..." }` — fetch, extract, embed, save. |
| `GET` | `/api/articles?q=` | List saved articles (newest first; optional search). |
| `GET` | `/api/articles/{id}` | Metadata + self-contained HTML (for the reader). |
| `GET` | `/api/articles/{id}/file?format=html\|md` | Download the saved file. |
| `GET` | `/api/articles/{id}/thumb` | Saved thumbnail image. |
| `DELETE` | `/api/articles/{id}` | Delete the article and its on-disk folder. |

## Notes & limitations

- Extraction works on **static HTML**. Heavily JavaScript-rendered pages may extract poorly — a future
  option is a headless-browser fetch (Playwright) behind the same extractor.
- Paywalled content cannot be retrieved.
- Re-saving the same URL currently creates a new entry.
- The archive location defaults to `./data` at the solution root; override with the `Archive:DataDir`
  configuration key (e.g. `dotnet run --Archive:DataDir=C:\my-archive`).
