using System.Text.Json;

namespace ComicReader.Infrastructure.Storage;

public sealed class LocalJsonCache
{
    private readonly JsonSerializerOptions _options =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

    public async Task<T?> ReadAsync<T>(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var path =
            Path.Combine(
                LocalDataPaths.Cache,
                fileName);

        if (!File.Exists(path))
            return default;

        try
        {
            await using var stream =
                File.OpenRead(path);

            return await JsonSerializer.DeserializeAsync<T>(
                stream,
                _options,
                cancellationToken);
        }
        catch
        {
            return default;
        }
    }

    public async Task WriteAsync<T>(
        string fileName,
        T value,
        CancellationToken cancellationToken = default)
    {
        var path =
            Path.Combine(
                LocalDataPaths.Cache,
                fileName);

        var temporaryPath =
            path + ".tmp";

        await using (
            var stream =
                File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                value,
                _options,
                cancellationToken);
        }

        File.Move(
            temporaryPath,
            path,
            true);
    }

    public bool Exists(string fileName)
    {
        return File.Exists(
            Path.Combine(
                LocalDataPaths.Cache,
                fileName));
    }
}
