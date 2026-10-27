namespace GroveGames.Database;

public interface IDatabaseInspector
{
    public string Directory { get; }
    public IReadOnlyCollection<string> OpenStoreNames { get; }
    public bool IsCollection(string name);
    public bool TryInspect(string name, out object? value);
    public bool TryWrite(string name, object value);
}
