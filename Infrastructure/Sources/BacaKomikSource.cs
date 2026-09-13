using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ComicReader.Core.Models;
using ComicReader.Core.Sources;

namespace ComicReader.Infrastructure.Sources;

public sealed class BacaKomikSource : IComicSource
{
    private const string RootUrl = "https://bacakomik.my";

    private readonly HttpClient _httpClient;

    public string Id => "bacakomik";
    public string Name => "BacaKomik";
    public string Language => "id";
    public string BaseUrl => RootUrl;

    public BacaKomikSource()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/140.0 Safari/537.36");

        _httpClient.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    public async Task<IReadOnlyList<Manga>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"{RootUrl}/?s={Uri.EscapeDataString(query)}";
        var html = await GetHtmlAsync(url, cancellationToken);

        return ParseMangaCards(html);
    }

    public async Task<Manga?> GetDetailsAsync(
        Manga manga,
        CancellationToken cancellationToken = default)
    {
        return await GetMangaAsync(manga.Url, cancellationToken);
    }

    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        Manga manga,
        CancellationToken cancellationToken = default)
    {
        var html = await GetHtmlAsync(manga.Url, cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var chapters = new List<Chapter>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var anchor in document.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = anchor.GetAttributeValue("href", "");
            var rawName = HtmlEntity.DeEntitize(
                anchor.InnerText ?? string.Empty);

            var name = CleanChapterName(rawName);

            if (string.IsNullOrWhiteSpace(href) ||
                string.IsNullOrWhiteSpace(name))
                continue;

            if (!name.Contains("chapter", StringComparison.OrdinalIgnoreCase))
                continue;

            var normalizedUrl = NormalizeUrl(href);

            if (!seen.Add(normalizedUrl))
                continue;

            var number = ExtractChapterNumber(name);

            chapters.Add(
                new Chapter(
                    Id,
                    normalizedUrl,
                    name,
                    null,
                    null,
                    number));
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
        var html = await GetHtmlAsync(chapter.Url, cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var pages = new List<Page>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var image in document.DocumentNode.SelectNodes("//img") ?? Enumerable.Empty<HtmlNode>())
        {
            var candidates = new[]
            {
                image.GetAttributeValue("data-src", ""),
                image.GetAttributeValue("data-lazy-src", ""),
                image.GetAttributeValue("data-original", ""),
                image.GetAttributeValue("src", "")
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var imageUrl = NormalizeUrl(candidate);

                if (!IsImageUrl(imageUrl))
                    continue;

                if (!seen.Add(imageUrl))
                    continue;

                pages.Add(
                    new Page(
                        pages.Count,
                        imageUrl,
                        new Dictionary<string, string>
                        {
                            ["Referer"] = chapter.Url,
                            ["User-Agent"] =
                                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                                "Chrome/140.0 Safari/537.36"
                        }));

                break;
            }
        }

        return pages;
    }

    private async Task<Manga?> GetMangaAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var html = await GetHtmlAsync(url, cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var title = FirstText(
            document,
            "//h1",
            "//h1[contains(@class,'entry-title')]",
            "//h1[contains(@class,'post-title')]",
            "//title");

        if (string.IsNullOrWhiteSpace(title))
            title = "Unknown";

        title = CleanText(title);

        var cover = FirstAttribute(
            document,
            "src",
            "//div[contains(@class,'thumb')]//img",
            "//div[contains(@class,'summary_image')]//img",
            "//div[contains(@class,'c-image-hover')]//img",
            "//img[contains(@class,'wp-post-image')]",
            "//img");

        cover = string.IsNullOrWhiteSpace(cover)
            ? null
            : NormalizeUrl(cover);

        var author = FindInfoValue(
            document,
            "author",
            "penulis",
            "author(s)");

        var artist = FindInfoValue(
            document,
            "artist",
            "artis",
            "ilustrator");

        var description = FindSynopsis(document);

        return new Manga(
            Id,
            NormalizeUrl(url),
            title,
            cover,
            author,
            artist,
            description);
    }

    private static string? FindSynopsis(HtmlDocument document)
    {
        // Pattern 1: heading containing Sinopsis followed by paragraph/div.
        var headings = document.DocumentNode.SelectNodes(
            "//h2 | //h3 | //h4 | //strong | //b");

        if (headings is not null)
        {
            foreach (var heading in headings)
            {
                var headingText = CleanText(
                    HtmlEntity.DeEntitize(heading.InnerText ?? ""));

                if (!headingText.Contains(
                        "sinopsis",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var node = heading.NextSibling;

                while (node is not null)
                {
                    if (node.NodeType == HtmlNodeType.Element)
                    {
                        var text = CleanText(
                            HtmlEntity.DeEntitize(node.InnerText ?? ""));

                        if (!string.IsNullOrWhiteSpace(text) &&
                            !text.Equals(
                                headingText,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return LimitDescription(text);
                        }
                    }

                    node = node.NextSibling;
                }

                // Some themes wrap synopsis in the next parent container.
                var parent = heading.ParentNode;

                if (parent is not null)
                {
                    var text = CleanText(
                        HtmlEntity.DeEntitize(parent.InnerText ?? ""));

                    var index = text.IndexOf(
                        "sinopsis",
                        StringComparison.OrdinalIgnoreCase);

                    if (index >= 0)
                    {
                        var after = text[(index + "sinopsis".Length)..];
                        after = CleanText(after);

                        if (!string.IsNullOrWhiteSpace(after))
                            return LimitDescription(after);
                    }
                }
            }
        }

        // Pattern 2: common synopsis containers.
        var selectors = new[]
        {
            "//*[contains(@class,'sinopsis')]",
            "//*[contains(@class,'synopsis')]",
            "//*[contains(@class,'description')]",
            "//*[contains(@class,'summary')]",
            "//*[contains(@class,'entry-content')]"
        };

        foreach (var selector in selectors)
        {
            foreach (var node in document.DocumentNode.SelectNodes(selector) ?? Enumerable.Empty<HtmlNode>())
            {
                var text = CleanText(
                    HtmlEntity.DeEntitize(node.InnerText ?? ""));

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (text.Contains("sinopsis", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("synopsis", StringComparison.OrdinalIgnoreCase))
                {
                    var index = text.IndexOf(
                        "sinopsis",
                        StringComparison.OrdinalIgnoreCase);

                    if (index >= 0)
                        text = text[(index + "sinopsis".Length)..];

                    return LimitDescription(text);
                }
            }
        }

        return null;
    }

    private static string? FindInfoValue(
        HtmlDocument document,
        params string[] labels)
    {
        foreach (var node in document.DocumentNode.SelectNodes(
                     "//li | //div | //p | //span | //td") ?? Enumerable.Empty<HtmlNode>())
        {
            var text = CleanText(
                HtmlEntity.DeEntitize(node.InnerText ?? ""));

            if (string.IsNullOrWhiteSpace(text))
                continue;

            foreach (var label in labels)
            {
                if (!text.StartsWith(
                        label,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = Regex.Replace(
                    text,
                    $"^{Regex.Escape(label)}\\s*:?\\s*",
                    "",
                    RegexOptions.IgnoreCase);

                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }

        return null;
    }

    private static string? FirstText(
        HtmlDocument document,
        params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var node = document.DocumentNode.SelectSingleNode(selector);

            if (node is null)
                continue;

            var text = CleanText(
                HtmlEntity.DeEntitize(node.InnerText ?? ""));

            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private static string? FirstAttribute(
        HtmlDocument document,
        string attribute,
        params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var node = document.DocumentNode.SelectSingleNode(selector);

            if (node is null)
                continue;

            var value = node.GetAttributeValue(attribute, "");

            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static IReadOnlyList<Manga> ParseMangaCards(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var results = new List<Manga>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var anchor in document.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = anchor.GetAttributeValue("href", "");

            if (!href.Contains("/komik/", StringComparison.OrdinalIgnoreCase))
                continue;

            if (href.Contains("/chapter-", StringComparison.OrdinalIgnoreCase))
                continue;

            var url = NormalizeUrl(href);

            if (!seen.Add(url))
                continue;

            var image = anchor.SelectSingleNode(".//img");

            var title =
                image?.GetAttributeValue("alt", "") ??
                image?.GetAttributeValue("title", "") ??
                CleanText(anchor.InnerText);

            title = CleanText(HtmlEntity.DeEntitize(title));

            if (string.IsNullOrWhiteSpace(title))
                continue;

            var cover =
                image?.GetAttributeValue("data-src", "") ??
                image?.GetAttributeValue("data-lazy-src", "") ??
                image?.GetAttributeValue("data-original", "") ??
                image?.GetAttributeValue("src", "");

            results.Add(
                new Manga(
                    "bacakomik",
                    url,
                    title,
                    string.IsNullOrWhiteSpace(cover)
                        ? null
                        : NormalizeUrl(cover)));

            if (results.Count >= 60)
                break;
        }

        return results;
    }

    private async Task<string> GetHtmlAsync(
        string url,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            url);

        request.Headers.Referrer =
            new Uri(RootUrl + "/");

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(
            cancellationToken);
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        return new Uri(
            new Uri(RootUrl + "/"),
            url).ToString();
    }

    private static bool IsImageUrl(string url)
    {
        return url.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
               url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               url.Contains(".png", StringComparison.OrdinalIgnoreCase) ||
               url.Contains(".webp", StringComparison.OrdinalIgnoreCase) ||
               url.Contains(".avif", StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractChapterNumber(string name)
    {
        var match = Regex.Match(
            name,
            @"(?:chapter|ch\.?)\s*(\d+(?:\.\d+)?)",
            RegexOptions.IgnoreCase);

        if (match.Success &&
            int.TryParse(
                match.Groups[1].Value.Split('.')[0],
                out var number))
            return number;

        return 0;
    }

    private static string CleanChapterName(string name)
    {
        name = CleanText(HtmlEntity.DeEntitize(name));

        // BacaKomik sometimes returns:
        // "Chapter Baru Chapter 179 End"
        // Make it simply:
        // "Chapter 179 End"
        name = Regex.Replace(
            name,
            @"^chapter\s+baru\s+",
            "Chapter ",
            RegexOptions.IgnoreCase);

        name = Regex.Replace(
            name,
            @"\s+",
            " ");

        return name.Trim();
    }

    private static string CleanText(string text)
    {
        return Regex.Replace(
            text.Replace("\u00A0", " "),
            @"\s+",
            " ").Trim();
    }

    private static string LimitDescription(string text)
    {
        text = CleanText(text);

        if (text.Length <= 5000)
            return text;

        return text[..5000].TrimEnd() + "...";
    }
}

