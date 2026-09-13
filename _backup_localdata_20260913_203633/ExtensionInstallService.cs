using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ComicReader.Infrastructure.Extensions;

namespace ComicReader.Infrastructure.Extensions;

public sealed class ExtensionInstallService
{
    private readonly HttpClient _httpClient;
    private readonly string _extensionDirectory;
    private readonly string _metadataFile;

    public ExtensionInstallService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateHttpClient();
        _extensionDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ComicReader", "extensions");
        _metadataFile = Path.Combine(_extensionDirectory, "installed.json");
    }

    public async Task InstallAsync(
        KeiyoushiExtension extension,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(extension.ApkUrl))
            throw new InvalidOperationException("APK URL tidak tersedia.");

        Directory.CreateDirectory(_extensionDirectory);

        using var response = await _httpClient.GetAsync(
            extension.ApkUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var safePackage = string.Concat(extension.PackageName.Select(c =>
            char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        var apkPath = Path.Combine(_extensionDirectory, safePackage + ".apk");

        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = File.Create(apkPath))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        var installed = ReadMetadata();
        installed.RemoveAll(x => string.Equals(x.PackageName, extension.PackageName, StringComparison.OrdinalIgnoreCase));
        installed.Add(new InstalledExtensionMetadata(
            extension.PackageName,
            extension.Name,
            extension.VersionCode,
            extension.VersionName,
            apkPath));

        await File.WriteAllTextAsync(
            _metadataFile,
            JsonSerializer.Serialize(installed, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    public IReadOnlyList<string> GetInstalledPackages() =>
        ReadMetadata().Select(x => x.PackageName).ToList();

    private List<InstalledExtensionMetadata> ReadMetadata()
    {
        try
        {
            if (!File.Exists(_metadataFile))
                return new List<InstalledExtensionMetadata>();

            var json = File.ReadAllText(_metadataFile);
            return JsonSerializer.Deserialize<List<InstalledExtensionMetadata>>(json)
                   ?? new List<InstalledExtensionMetadata>();
        }
        catch
        {
            return new List<InstalledExtensionMetadata>();
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ComicReader", "0.1"));

        return client;
    }

    private sealed record InstalledExtensionMetadata(
        string PackageName,
        string Name,
        long VersionCode,
        string VersionName,
        string ApkPath);
}
