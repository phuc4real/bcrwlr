using System.IO.Compression;
using System.Net;
using Bcrwlr.Api.Data;
using Bcrwlr.Api.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Archive paths (data dir + SQLite location) are needed before the DbContext is registered.
var paths = new ArchivePaths(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(paths);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={paths.DbPath}"));

// Shared HttpClient for fetching pages and images: realistic UA, redirects, decompression.
builder.Services.AddHttpClient("fetch", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/124.0.0.0 Safari/537.36 bcrwlr/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,image/*,*/*");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 10,
    AutomaticDecompression = DecompressionMethods.All,
});

builder.Services.AddSingleton<ArticleExtractor>();
builder.Services.AddSingleton<ImageEmbedder>();
builder.Services.AddScoped<ArticleStore>();
builder.Services.AddScoped<ArchiveService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Saved articles inline images as base64, so reader/list responses can be multiple MB.
// Compress them (the small CPU cost is worth the bandwidth on JSON + HTML payloads).
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes
        .Concat(["application/json", "text/html", "text/markdown"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

const string DevCors = "dev-spa";
builder.Services.AddCors(options => options.AddPolicy(DevCors, policy =>
    policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Create the SQLite schema on first run (simple single-table schema; no migrations needed).
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
    app.UseCors(DevCors);
else
    app.UseExceptionHandler(); // returns ProblemDetails for unhandled errors

app.UseResponseCompression();

// Serve the built React SPA from wwwroot in production.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHealthChecks("/healthz");
app.MapFallbackToFile("index.html");

app.Run();
