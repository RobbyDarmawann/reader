using System.Text.Json;
using ComicReader.Core.Models;
using ComicReader.Core.Sources;

namespace ComicReader.Infrastructure.Sources;

public sealed class ShinigamiSource : IComicSource, IComicCatalogSource
{
    private const string ApiRoot = "https://api.shngm.io";
    private readonly HttpClient _client;

    public ShinigamiSource()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "ComicReader/1.0 (desktop; native source adapter)");
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("DNT", "1");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://11.shinigami.asia");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-GPC", "1");
    }

    public string Id => "shinigami";
    public string Name => "Shinigami";
    public string Language => "id";
    public string BaseUrl => "https://11.shinigami.asia";

    public IReadOnlyList<SourceCatalogFilter> Filters { get; } = new[]
    {
        new SourceCatalogFilter("genre", "Genre", new[]
        {
            "All", "Action", "Adventure", "Comedy", "Drama", "Fantasy", "Horror",
            "Isekai", "Magic", "Martial Arts", "Mystery", "Romance", "School Life",
            "Seinen", "Shoujo", "Shounen", "Slice of Life", "Sports", "Supernatural",
            "Thriller", "Tragedy", "Wuxia"
        }),
        new SourceCatalogFilter("type", "Type", new[] { "All", "Manga", "Manhwa", "Manhua" }),
        new SourceCatalogFilter("status", "Status", new[] { "All", "Ongoing", "Completed", "Hiatus" }),
        new SourceCatalogFilter("format", "Format", new[] { "All", "Color", "Black and White" }),
        new SourceCatalogFilter("sort", "Sort", new[] { "Popular", "Latest" })
    };

    public Task<IReadOnlyList<Manga>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        GetMangaListAsync(BuildListUrl(query, 1, null), cancellationToken);

    public Task<IReadOnlyList<Manga>> GetCatalogAsync(
        SourceCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        var sort = string.Equals(request.Section, "latest", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(request.Sort, "Latest", StringComparison.OrdinalIgnoreCase)
            ? "latest"
            : "popularity";

        return GetMangaListAsync(
            BuildListUrl(null, request.Page, sort, request),
            cancellationToken);
    }

    public async Task<Manga?> GetDetailsAsync(
        Manga manga,
        CancellationToken cancellationToken = default)
    {
        using var document = await GetJsonAsync(
            $"{ApiRoot}/v1/manga/detail/{Uri.EscapeDataString(manga.Url)}",
            cancellationToken);

        var data = Property(document.RootElement, "data");
        if (data.ValueKind == JsonValueKind.Undefined)
            return manga;

        var taxonomy = Property(data, "taxonomy");
        var genres = GetTaxonomyNames(taxonomy, "Genre");
        var type = GetTaxonomyNames(taxonomy, "Format");
        var author = GetTaxonomyNames(taxonomy, "Author");
        var artist = GetTaxonomyNames(taxonomy, "Artist");
        var statusNumber = GetInt(data, "status");

        return manga with
        {
            Author = author,
            Artist = artist,
            Description = GetString(data, "description") ?? manga.Description,
            Status = statusNumber switch
            {
                1 => "Ongoing",
                2 => "Completed",
                3 => "Hiatus",
                _ => "Unknown"
            },
            Type = type,
            Genres = genres.Split(", ", StringSplitOptions.RemoveEmptyEntries)
        };
    }

    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        Manga manga,
        CancellationToken cancellationToken = default)
    {
        using var document = await GetJsonAsync(
            $"{ApiRoot}/v1/chapter/{Uri.EscapeDataString(manga.Url)}/list?page_size=3000",
            cancellationToken);

        var list = Property(document.RootElement, "data");
        var result = new List<Chapter>();

        if (list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                var id = GetString(item, "chapter_id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var number = GetDouble(item, "chapter_number");
                var title = GetString(item, "chapter_title") ?? string.Empty;
                var name = $"Chapter {number:0.##} {title}".Trim();
                result.Add(new Chapter(Id, id, name, Number: (int)number));
            }
        }

        return result.OrderByDescending(x => x.Number).ToArray();
    }

    public async Task<IReadOnlyList<Page>> GetPagesAsync(
        Chapter chapter,
        CancellationToken cancellationToken = default)
    {
        using var document = await GetJsonAsync(
            $"{ApiRoot}/v1/chapter/detail/{Uri.EscapeDataString(chapter.Url)}",
            cancellationToken);

        var pageList = Property(document.RootElement, "data");
        var baseUrl = GetString(pageList, "base_url") ?? string.Empty;
        var chapterPage = Property(pageList, "chapter");
        var path = GetString(chapterPage, "path") ?? string.Empty;
        var pages = Property(chapterPage, "data");

        if (pages.ValueKind != JsonValueKind.Array)
            return Array.Empty<Page>();

        return pages.EnumerateArray()
            .Select((item, index) => new Page(
                index,
                $"{baseUrl}{path}{item.GetString()}",
                new Dictionary<string, string>
                {
                    ["Referer"] = BaseUrl,
                    ["Accept"] = "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8"
                }))
            .ToArray();
    }

    private async Task<IReadOnlyList<Manga>> GetMangaListAsync(
        string url,
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(url, cancellationToken);
        var data = Property(document.RootElement, "data");
        if (data.ValueKind != JsonValueKind.Array)
            return Array.Empty<Manga>();

        return data.EnumerateArray()
            .Select(item => new Manga(
                Id,
                GetString(item, "manga_id") ?? string.Empty,
                GetString(item, "title") ?? "Untitled",
                GetString(item, "cover_portrait_url") ??
                GetString(item, "cover_image_url")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .ToArray();
    }

    private string BuildListUrl(
        string? query,
        int page,
        string? sort,
        SourceCatalogRequest? request = null)
    {
        var queryParts = new List<string>
        {
            $"page={Math.Max(1, page)}",
            "page_size=30"
        };

        if (!string.IsNullOrWhiteSpace(query))
            queryParts.Add($"q={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(sort))
            queryParts.Add($"sort={Uri.EscapeDataString(sort)}");
        if (request is not null)
        {
            AddIfSelected(queryParts, "status", request.Status);
            AddIfSelected(queryParts, "format", request.Format);
            AddIfSelected(queryParts, "type", request.Type);
            AddIfSelected(queryParts, "genre", request.Genre);
        }

        return $"{ApiRoot}/v1/manga/list?{string.Join("&", queryParts)}";
    }

    private static void AddIfSelected(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            !string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"{name}={Uri.EscapeDataString(value.ToLowerInvariant())}");
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static JsonElement Property(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value))
            return value;
        return default;
    }

    private static string? GetString(JsonElement element, string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int GetInt(JsonElement element, string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : 0;
    }

    private static double GetDouble(JsonElement element, string name)
    {
        var value = Property(element, name);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            return number;
        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number)
            ? number
            : 0;
    }

    private static string GetTaxonomyNames(JsonElement taxonomy, string key)
    {
        var values = Property(taxonomy, key);
        return values.ValueKind == JsonValueKind.Array
            ? string.Join(", ", values.EnumerateArray().Select(x => GetString(x, "name")).Where(x => !string.IsNullOrWhiteSpace(x)))
            : string.Empty;
    }
}
