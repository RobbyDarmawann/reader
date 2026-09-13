using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Data;
using ComicReader.Models;

using Microsoft.EntityFrameworkCore;

namespace ComicReader.Services;

public class DatabaseService
{
    private readonly string _databasePath;

    public DatabaseService()
    {
        var appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        var comicReaderPath = Path.Combine(
            appDataPath,
            "ComicReader");

        Directory.CreateDirectory(comicReaderPath);

        _databasePath = Path.Combine(
            comicReaderPath,
            "comicreader.db");
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();

        await db.Database.EnsureCreatedAsync();

        await SeedDemoDataAsync();
    }

    public async Task<List<Manga>> GetLibraryAsync()
    {
        await using var db = CreateContext();

        return await db.Manga
            .Where(m => m.InLibrary)
            .OrderBy(m => m.Title)
            .ToListAsync();
    }

    public async Task AddToLibraryAsync(Manga manga)
    {
        await using var db = CreateContext();

        manga.InLibrary = true;
        manga.DateAdded = DateTime.UtcNow;

        db.Manga.Add(manga);

        await db.SaveChangesAsync();
    }

    public async Task RemoveFromLibraryAsync(int mangaId)
    {
        await using var db = CreateContext();

        var manga = await db.Manga
            .FirstOrDefaultAsync(m => m.Id == mangaId);

        if (manga == null)
            return;

        manga.InLibrary = false;

        await db.SaveChangesAsync();
    }

    private async Task SeedDemoDataAsync()
    {
        await using var db = CreateContext();

        if (await db.Manga.AnyAsync())
            return;

        var mangaList = new List<Manga>
        {
            new Manga
            {
                Title = "One Piece",
                Author = "Eiichiro Oda",
                Artist = "Eiichiro Oda",
                Description = "A pirate adventure.",
                SourceName = "Demo Source",
                SourceId = "demo",
                Url = "https://example.com/one-piece",
                InLibrary = true
            },

            new Manga
            {
                Title = "Naruto",
                Author = "Masashi Kishimoto",
                Artist = "Masashi Kishimoto",
                Description = "The story of a young ninja.",
                SourceName = "Demo Source",
                SourceId = "demo",
                Url = "https://example.com/naruto",
                InLibrary = true
            },

            new Manga
            {
                Title = "Bleach",
                Author = "Tite Kubo",
                Artist = "Tite Kubo",
                Description = "A supernatural action series.",
                SourceName = "Demo Source",
                SourceId = "demo",
                Url = "https://example.com/bleach",
                InLibrary = true
            }
        };

        db.Manga.AddRange(mangaList);

        await db.SaveChangesAsync();
    }
}