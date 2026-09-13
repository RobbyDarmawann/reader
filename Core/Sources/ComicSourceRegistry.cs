namespace ComicReader.Core.Sources;

public sealed class ComicSourceRegistry
{
    private readonly Dictionary<string, IComicSource> _sources =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IComicSource> Sources =>
        _sources.Values.ToArray();

    public void Register(IComicSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _sources[source.Id] = source;
    }

    public bool Unregister(string sourceId)
    {
        return _sources.Remove(sourceId);
    }

    public bool TryGet(
        string sourceId,
        out IComicSource? source)
    {
        return _sources.TryGetValue(sourceId, out source);
    }

    public void Clear()
    {
        _sources.Clear();
    }
}
