using System.Text.Json;

namespace ComicReader.Infrastructure.Storage;

public sealed class UserPreferences
{
    public string Theme { get; set; } = "Dark";
    public string ReadingMode { get; set; } = "Vertical";
    public string DownloadFolder { get; set; } = string.Empty;
}

public sealed class UserPreferencesService
{
    private readonly string _filePath;

    public UserPreferencesService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ComicReader");

        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "preferences.json");
    }

    public UserPreferences Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                return JsonSerializer.Deserialize<UserPreferences>(
                           File.ReadAllText(_filePath))
                       ?? new UserPreferences();
            }
        }
        catch
        {
        }

        return new UserPreferences();
    }

    public void Save(UserPreferences preferences)
    {
        var json = JsonSerializer.Serialize(
            preferences,
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(_filePath, json);
    }
}