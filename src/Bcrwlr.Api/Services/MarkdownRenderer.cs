using System.Text;
using ReverseMarkdown;
using SmartReader;

namespace Bcrwlr.Api.Services;

/// <summary>
/// Converts extracted content HTML to GitHub-flavored Markdown (via ReverseMarkdown) and
/// prepends YAML frontmatter. Expects content whose image paths are already localized
/// (<c>images/&lt;file&gt;</c>) so the saved <c>.md</c> references its sibling image files.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly Converter Converter = new(new Config
    {
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true,
        UnknownTags = Config.UnknownTagsOption.Bypass,
    });

    public static string Render(Article article, Uri sourceUri, string localizedContentHtml, DateTime savedAt)
    {
        var body = Converter.Convert(localizedContentHtml);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.Append("title: ").AppendLine(YamlString(article.Title));
        if (!string.IsNullOrWhiteSpace(article.Author ?? article.Byline))
            sb.Append("author: ").AppendLine(YamlString((article.Author ?? article.Byline)!.Trim()));
        if (!string.IsNullOrWhiteSpace(article.SiteName))
            sb.Append("site: ").AppendLine(YamlString(article.SiteName));
        sb.Append("source: ").AppendLine(YamlString(sourceUri.ToString()));
        if (article.PublicationDate is { } pub)
            sb.Append("published: ").AppendLine(pub.ToString("yyyy-MM-dd"));
        sb.Append("saved: ").AppendLine(savedAt.ToString("yyyy-MM-dd"));
        if (article.TimeToRead.TotalMinutes >= 1)
            sb.Append("reading_minutes: ").AppendLine(((int)Math.Ceiling(article.TimeToRead.TotalMinutes)).ToString());
        sb.AppendLine("---");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(article.Title))
            sb.Append("# ").AppendLine(article.Title.Trim()).AppendLine();

        sb.Append(body);
        return sb.ToString();
    }

    private static string YamlString(string value)
        => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
