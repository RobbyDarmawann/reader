namespace ComicReader.Infrastructure.Extensions;

public sealed record KeiyoushiRepository(
    string Name,
    string ShortName,
    string SigningKeyFingerprint,
    string? Website,
    string? Discord,
    IReadOnlyList<KeiyoushiExtension> Extensions);

public sealed record KeiyoushiExtension(
    string Name,
    string PackageName,
    string LibraryVersion,
    long VersionCode,
    string VersionName,
    int Nsfw,
    string? ApkUrl,
    string? IconUrl,
    string? JarUrl,
    IReadOnlyList<KeiyoushiSource> Sources);

public sealed record KeiyoushiSource(
    ulong Id,
    string Name,
    string Language,
    string BaseUrl,
    IReadOnlyList<string> AlternateBaseUrls);
