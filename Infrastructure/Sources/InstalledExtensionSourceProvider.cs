using ComicReader.Core.Sources;
using ComicReader.Infrastructure.Extensions;

namespace ComicReader.Infrastructure.Sources;

public sealed class InstalledExtensionSourceProvider
{
    private readonly ExtensionInstallService _installService;

    public InstalledExtensionSourceProvider(
        ExtensionInstallService? installService = null)
    {
        _installService =
            installService ?? new ExtensionInstallService();
    }

    public IReadOnlyList<ComicSourceDescriptor> GetInstalledSources(
        IEnumerable<KeiyoushiExtension> extensions)
    {
        var installed =
            _installService
                .GetInstalledPackages()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return extensions
            .Where(extension =>
                installed.Contains(extension.PackageName))
            .SelectMany(extension =>
                extension.Sources.Select(source =>
                    new ComicSourceDescriptor(
                        source.Id.ToString(),
                        source.Name,
                        source.Language,
                        source.BaseUrl,
                        extension.PackageName,
                        extension.VersionName)))
            .OrderBy(source =>
                source.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
