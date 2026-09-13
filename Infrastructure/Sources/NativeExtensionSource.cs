using HtmlAgilityPack;
using ComicReader.Core.Models;
using ComicReader.Core.Sources;

namespace ComicReader.Infrastructure.Sources;

// Desktop-native HTML adapter for extensions that expose a web source URL.
public sealed class NativeExtensionSource : IComicSource
{
    private readonly ComicSourceDescriptor _descriptor;
    private readonly HttpClient _client;

    public NativeExtensionSource(ComicSourceDescriptor descriptor)
    {
        _descriptor = descriptor;
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ComicReader/1.0");
    }

    public string Id => _descriptor.Id;
    public string Name => _descriptor.Name;
    public string Language => _descriptor.Language;
    public string BaseUrl => _descriptor.BaseUrl;

    public async Task<IReadOnlyList<Manga>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl($"?s={Uri.EscapeDataString(query)}");
        return ParseMangaCards(await GetHtmlAsync(url, cancellationToken));
    }

    public async Task<Manga?> GetDetailsAsync(
        Manga manga,
        CancellationToken cancellationToken = default)
    {
        var document = Load(await GetHtmlAsync(manga.Url, cancellationToken));
        var title = FirstText(document, "//h1", "//title") ?? manga.Title;
        var cover = FirstImage(document);
        var description = FirstText(
            document,
            "//*[contains(@class,'summary')]",
            "//*[contains(@class,'description')]",
            "//*[contains(@class,'synopsis')]");

        return manga with
        {
            Title = Clean(title),
            CoverUrl = cover ?? manga.CoverUrl,
            Description = Clean(description ?? manga.Description ?? string.Empty)
        };
    }

    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        Manga manga,
        CancellationToken cancellationToken = default)
    {
        var document = Load(await GetHtmlAsync(manga.Url, cancellationToken));
        var chapters = new List<Chapter>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var anchor in document.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var name = Clean(anchor.InnerText);
            var url = NormalizeUrl(anchor.GetAttributeValue("href", string.Empty));

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(url) ||
                !IsChapterName(name) ||
                !seen.Add(url))
                continue;

            chapters.Add(new Chapter(Id, url, name, Number: ExtractNumber(name)));
        }

        return chapters
            .OrderByDescending(x => x.Number)
            .ThenByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<Page>> GetPagesAsync(
        Chapter chapter,
        CancellationToken cancellationToken = default)
    {
        var document = Load(await GetHtmlAsync(chapter.Url, cancellationToken));
        var pages = new List<Page>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var image in document.DocumentNode.SelectNodes("//img") ?? Enumerable.Empty<HtmlNode>())
        {
            var candidate = new[]
            {
                image.GetAttributeValue("data-src", string.Empty),
                image.GetAttributeValue("data-lazy-src", string.Empty),
                image.GetAttributeValue("data-original", string.Empty),
                image.GetAttributeValue("src", string.Empty)
            }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            var url = NormalizeUrl(candidate ?? string.Empty);
            if (!IsImageUrl(url) || !seen.Add(url))
                continue;

            pages.Add(new Page(
                pages.Count,
                url,
                new Dictionary<string, string> { ["Referer"] = chapter.Url }));
        }

        return pages;
    }

    private async Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private IReadOnlyList<Manga> ParseMangaCards(string html)
    {
        var document = Load(html);
        var result = new List<Manga>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var anchor in document.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var url = NormalizeUrl(anchor.GetAttributeValue("href", string.Empty));
            var title = Clean(anchor.InnerText);
            var image = anchor.Descendants("img").FirstOrDefault();
            var cover = NormalizeUrl(image?.GetAttributeValue("data-src", string.Empty)
                                     ?? image?.GetAttributeValue("src", string.Empty)
                                     ?? string.Empty);

            if (string.IsNullOrWhiteSpace(title) ||
                !Uri.TryCreate(url, UriKind.Absolute, out _) ||
                !seen.Add(url) ||
                title.Length < 2)
                continue;

            result.Add(new Manga(Id, url, title, IsImageUrl(cover) ? cover : null));
        }

        return result.Take(100).ToArray();
    }

    private string BuildUrl(string suffix)
    {
        var root = BaseUrl.TrimEnd('/');
        return suffix.StartsWith('/') ? root + suffix : root + "/" + suffix;
    }

    private string NormalizeUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Uri.TryCreate(new Uri(BuildUrl("/")), value, out var uri)
            ? uri.AbsoluteUri
            : string.Empty;
    }

    private static HtmlDocument Load(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document;
    }

    private string? FirstImage(HtmlDocument document)
    {
        var image = document.DocumentNode.SelectSingleNode("//img");
        return NormalizeUrl(image?.GetAttributeValue("data-src", string.Empty)
                            ?? image?.GetAttributeValue("src", string.Empty)
                            ?? string.Empty);
    }

    private static string? FirstText(HtmlDocument document, params string[] selectors) =>
        selectors.Select(selector => document.DocumentNode.SelectSingleNode(selector)?.InnerText)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

    private static bool IsChapterName(string name) =>
        name.Contains("chapter", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ch.", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ep.", StringComparison.OrdinalIgnoreCase);

    private static bool IsImageUrl(string url) =>
        url.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
        url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        url.Contains(".png", StringComparison.OrdinalIgnoreCase) ||
        url.Contains(".webp", StringComparison.OrdinalIgnoreCase);

    private static int ExtractNumber(string text)
    {
        var digits = new string(text.SkipWhile(c => !char.IsDigit(c))
            .TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : 0;
    }

    private static string Clean(string? text) =>
        HtmlEntity.DeEntitize(text ?? string.Empty)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();
}