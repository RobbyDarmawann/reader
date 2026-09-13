namespace ComicReader.Core.Sources;

public sealed record ComicSourceDescriptor(
    string Id,
    string Name,
    string Language,
    string BaseUrl,
    string ExtensionPackage,
    string ExtensionVersion,
    IReadOnlyList<string>? AlternateBaseUrls = null);
