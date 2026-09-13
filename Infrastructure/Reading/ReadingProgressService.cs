using Microsoft.Data.Sqlite;

namespace ComicReader.Infrastructure.Reading;

public sealed record ReadingProgress(
    string MangaUrl,
    string ChapterUrl,
    string MangaTitle,
    string ChapterName,
    double ScrollOffset,
    double ScrollMaximum,
    DateTimeOffset UpdatedAt,
    int PageIndex = 0,
    double PageProgress = 0,
    string? CoverUrl = null);

public sealed class ReadingProgressService
{
    private readonly string _databasePath;

    public ReadingProgressService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ComicReader");

        Directory.CreateDirectory(directory);

        _databasePath =
            Path.Combine(directory, "comicreader.db");

        Initialize();
    }

    private void Initialize()
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ReadingProgress (
                MangaUrl TEXT NOT NULL,
                ChapterUrl TEXT NOT NULL,
                MangaTitle TEXT NOT NULL,
                ChapterName TEXT NOT NULL,
                ScrollOffset REAL NOT NULL DEFAULT 0,
                ScrollMaximum REAL NOT NULL DEFAULT 0,
                UpdatedAt TEXT NOT NULL,
                PageIndex INTEGER NOT NULL DEFAULT 0,
                PageProgress REAL NOT NULL DEFAULT 0,
                PRIMARY KEY (MangaUrl, ChapterUrl)
            );
            """;

        command.ExecuteNonQuery();

        // Migrasi database lama.
        TryAddColumn(
            connection,
            "PageIndex",
            "INTEGER NOT NULL DEFAULT 0");

        TryAddColumn(
            connection,
            "PageProgress",
            "REAL NOT NULL DEFAULT 0");

        TryAddColumn(
            connection,
            "CoverUrl",
            "TEXT NULL");
    }

    private static void TryAddColumn(
        SqliteConnection connection,
        string column,
        string definition)
    {
        try
        {
            using var command =
                connection.CreateCommand();

            command.CommandText =
                $"ALTER TABLE ReadingProgress " +
                $"ADD COLUMN {column} {definition};";

            command.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Kolom sudah ada.
        }
    }

    public void Save(
        string mangaUrl,
        string chapterUrl,
        string mangaTitle,
        string chapterName,
        double scrollOffset,
        double scrollMaximum,
        string? coverUrl = null)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO ReadingProgress
            (
                MangaUrl,
                ChapterUrl,
                MangaTitle,
                ChapterName,
                ScrollOffset,
                ScrollMaximum,
                UpdatedAt,
                CoverUrl
            )
            VALUES
            (
                $mangaUrl,
                $chapterUrl,
                $mangaTitle,
                $chapterName,
                $scrollOffset,
                $scrollMaximum,
                $updatedAt,
                $coverUrl
            )
            ON CONFLICT(MangaUrl, ChapterUrl)
            DO UPDATE SET
                MangaTitle = excluded.MangaTitle,
                ChapterName = excluded.ChapterName,
                ScrollOffset = excluded.ScrollOffset,
                ScrollMaximum = excluded.ScrollMaximum,
                UpdatedAt = excluded.UpdatedAt,
                CoverUrl = excluded.CoverUrl;
            """;

        command.Parameters.AddWithValue(
            "$mangaUrl",
            mangaUrl);

        command.Parameters.AddWithValue(
            "$chapterUrl",
            chapterUrl);

        command.Parameters.AddWithValue(
            "$mangaTitle",
            mangaTitle);

        command.Parameters.AddWithValue(
            "$chapterName",
            chapterName);

        command.Parameters.AddWithValue(
            "$scrollOffset",
            scrollOffset);

        command.Parameters.AddWithValue(
            "$scrollMaximum",
            scrollMaximum);

        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTimeOffset.UtcNow.ToString("O"));

        command.Parameters.AddWithValue(
            "$coverUrl",
            (object?)coverUrl ?? DBNull.Value);

        command.ExecuteNonQuery();
    }

    public ReadingProgress? Get(
        string mangaUrl,
        string chapterUrl)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                MangaUrl,
                ChapterUrl,
                MangaTitle,
                ChapterName,
                ScrollOffset,
                ScrollMaximum,
                UpdatedAt,
                CoverUrl
            FROM ReadingProgress
            WHERE MangaUrl = $mangaUrl
              AND ChapterUrl = $chapterUrl
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$mangaUrl",
            mangaUrl);

        command.Parameters.AddWithValue(
            "$chapterUrl",
            chapterUrl);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return null;

        return new ReadingProgress(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDouble(4),
            reader.GetDouble(5),
            DateTimeOffset.Parse(reader.GetString(6)),
            CoverUrl: reader.IsDBNull(7) ? null : reader.GetString(7));
    }

    public ReadingProgress? GetLatest(
        string mangaUrl)
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                MangaUrl,
                ChapterUrl,
                MangaTitle,
                ChapterName,
                ScrollOffset,
                ScrollMaximum,
                UpdatedAt,
                CoverUrl
            FROM ReadingProgress
            WHERE MangaUrl = $mangaUrl
            ORDER BY UpdatedAt DESC
            LIMIT 1;
            """;

        command.Parameters.AddWithValue("$mangaUrl", mangaUrl);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return null;

        return new ReadingProgress(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDouble(4),
            reader.GetDouble(5),
            DateTimeOffset.Parse(reader.GetString(6)),
            CoverUrl: reader.IsDBNull(7) ? null : reader.GetString(7));
    }
    public IReadOnlyList<ReadingProgress> GetAllLatest()
    {
        using var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                MangaUrl,
                ChapterUrl,
                MangaTitle,
                ChapterName,
                ScrollOffset,
                ScrollMaximum,
                UpdatedAt,
                CoverUrl
            FROM ReadingProgress
            WHERE rowid IN
            (
                SELECT rowid
                FROM ReadingProgress AS rp2
                WHERE rp2.MangaUrl = ReadingProgress.MangaUrl
                ORDER BY UpdatedAt DESC
                LIMIT 1
            )
            ORDER BY UpdatedAt DESC;
            """;

        using var reader = command.ExecuteReader();

        var result = new List<ReadingProgress>();

        while (reader.Read())
        {
            result.Add(
                new ReadingProgress(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetDouble(4),
                    reader.GetDouble(5),
                    DateTimeOffset.Parse(reader.GetString(6)),
                    CoverUrl: reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return result;
    }
    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(
            $"Data Source={_databasePath}");
    }
}




