using ComicReader.Models;
using Microsoft.EntityFrameworkCore;

namespace ComicReader.Data;

public class AppDbContext : DbContext
{
    public DbSet<Manga> Manga => Set<Manga>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
}