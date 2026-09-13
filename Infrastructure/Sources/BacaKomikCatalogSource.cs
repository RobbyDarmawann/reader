using HtmlAgilityPack;
using ComicReader.Core.Models;
using ComicReader.Core.Sources;

namespace ComicReader.Infrastructure.Sources;

public sealed class BacaKomikCatalogSource : IComicCatalogSource
{
    private const string RootUrl = "https://bacakomik.my";

    private readonly HttpClient _httpClient;

    public BacaKomikCatalogSource(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public IReadOnlyList<SourceCatalogFilter> Filters { get; } =
        new[]
        {
            new SourceCatalogFilter(
                "genre",
                "Genre",
                new[]
                {
                    "All",
                    "Action",
                    "Adventure",
                    "Comedy",
                    "Crime",
                    "Demons",
                    "Drama",
                    "Ecchi",
                    "Fantasy",
                    "Harem",
                    "Historical",
                    "Horror",
                    "Isekai",
                    "Josei",
                    "Magic",
                    "Martial Arts",
                    "Mecha",
                    "Mystery",
                    "Psychological",
                    "Reincarnation",
                    "Romance",
                    "School",
                    "Seinen",
                    "Shoujo",
                    "Shounen",
                    "Slice of Life",
                    "Sports",
                    "Supernatural",
                    "Thriller",
                    "Tragedy",
                    "Wuxia",
                    "Yuri"
                }),

            new SourceCatalogFilter(
                "type",
                "Type",
                new[]
                {
                    "All",
                    "Manga",
                    "Manhwa",
                    "Manhua",
                    "Comic"
                }),

            new SourceCatalogFilter(
                "status",
                "Status",
                new[]
                {
                    "All",
                    "Ongoing",
                    "Completed",
                    "Hiatus"
                }),

            new SourceCatalogFilter(
                "format",
                "Format",
                new[]
                {
                    "All",
                    "Hitam Putih",
                    "Berwarna"
                }),

            new SourceCatalogFilter(
                "sort",
                "Sort",
                new[]
                {
                    "Popular",
                    "Latest Update",
                    "Latest Added",
                    "A-Z",
                    "Z-A"
                })
        };

    public async Task<IReadOnlyList<Manga>> GetCatalogAsync(
        SourceCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(request);

        using var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(
            cancellationToken);

        return ParseMangaCards(html);
    }

    private static string BuildUrl(
        SourceCatalogRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Genre) &&
            !string.Equals(
                request.Genre,
                "All",
                StringComparison.OrdinalIgnoreCase))
        {
            var slug = Slugify(request.Genre);
            return $"{RootUrl}/genres/{slug}/";
        }

        if (!string.IsNullOrWhiteSpace(request.Type) &&
            !string.Equals(
                request.Type,
                "All",
                StringComparison.OrdinalIgnoreCase))
        {
            return request.Type.ToLowerInvariant() switch
            {
                "manga" =>
                    $"{RootUrl}/baca-manga/",

                "manhwa" =>
                    $"{RootUrl}/baca-manhwa/",

                "manhua" =>
                    $"{RootUrl}/baca-manhua/",

                _ =>
                    $"{RootUrl}/daftar-komik/"
            };
        }

        if (string.Equals(
                request.Section,
                "latest",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{RootUrl}/komik-terbaru/";
        }

        if (string.Equals(
                request.Sort,
                "Latest Update",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{RootUrl}/komik-terbaru/";
        }

        if (string.Equals(
                request.Sort,
                "Latest Added",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{RootUrl}/daftar-komik/";
        }

        if (string.Equals(
                request.Sort,
                "A-Z",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{RootUrl}/daftar-komik/";
        }

        if (string.Equals(
                request.Sort,
                "Z-A",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{RootUrl}/daftar-komik/";
        }

        return $"{RootUrl}/komik-populer/";
    }

    private static IReadOnlyList<Manga> ParseMangaCards(
        string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var results = new List<Manga>();
        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var anchor in document.DocumentNode
                     .SelectNodes("//a[@href]") ??
                 Enumerable.Empty<HtmlNode>())
        {
            var href =
                anchor.GetAttributeValue(
                    "href",
                    string.Empty)
                .Trim();

            if (!href.Contains(
                    "/komik/",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (href.Contains(
                    "/chapter-",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (href.Contains(
                    "/genres/",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = NormalizeUrl(href);

            if (!seen.Add(url))
                continue;

            var image =
                anchor.SelectSingleNode(".//img");

            var title =
                GetTitle(anchor, image);

            if (string.IsNullOrWhiteSpace(title))
                continue;

            var cover =
                GetImageUrl(image);

            results.Add(
                new Manga(
                    "bacakomik",
                    url,
                    title,
                    cover));

            if (results.Count >= 60)
                break;
        }

        return results;
    }

    private static string GetTitle(
        HtmlNode anchor,
        HtmlNode? image)
    {
        var alt =
            image?.GetAttributeValue(
                "alt",
                string.Empty);

        if (!string.IsNullOrWhiteSpace(alt))
            return CleanTitle(alt);

        var titleAttribute =
            anchor.GetAttributeValue(
                "title",
                string.Empty);

        if (!string.IsNullOrWhiteSpace(titleAttribute))
            return CleanTitle(titleAttribute);

        var text =
            HtmlEntity.DeEntitize(
                anchor.InnerText);

        return CleanTitle(text);
    }

    private static string? GetImageUrl(
        HtmlNode? image)
    {
        if (image is null)
            return null;

        var candidates = new[]
        {
            image.GetAttributeValue(
                "data-src",
                string.Empty),

            image.GetAttributeValue(
                "data-lazy-src",
                string.Empty),

            image.GetAttributeValue(
                "data-original",
                string.Empty),

            image.GetAttributeValue(
                "src",
                string.Empty)
        };

        foreach (var value in candidates)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                !value.StartsWith(
                    "data:",
                    StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeUrl(value);
            }
        }

        return null;
    }

    private static string CleanTitle(
        string value)
    {
        var title =
            HtmlEntity.DeEntitize(value)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

        while (title.Contains("  "))
            title = title.Replace("  ", " ");

        return title;
    }

    private static string NormalizeUrl(
        string url)
    {
        if (Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var absolute))
        {
            return absolute.ToString();
        }

        return new Uri(
            new Uri(RootUrl),
            url).ToString();
    }

    private static string Slugify(
        string value)
    {
        var chars =
            value.Trim()
                .ToLowerInvariant()
                .Select(
                    c => char.IsLetterOrDigit(c)
                        ? c
                        : '-')
                .ToArray();

        var result =
            new string(chars);

        while (result.Contains("--"))
            result = result.Replace("--", "-");

        return result.Trim('-');
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "ComicReader/0.1");

        return client;
    }
}
