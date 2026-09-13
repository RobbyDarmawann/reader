using System;

namespace ComicReader.Models;

public class Manga
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Author { get; set; }

    public string? Artist { get; set; }

    public string? Description { get; set; }

    public string? CoverUrl { get; set; }

    public string? SourceId { get; set; }

    public string? SourceName { get; set; }

    public string? Url { get; set; }

    public bool InLibrary { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
}