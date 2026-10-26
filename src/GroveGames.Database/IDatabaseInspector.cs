namespace GroveGames.Database;

public interface IDatabaseInspector
{
    public string Directory { get; }
    public IReadOnlyCollection<string> OpenStoreNames { get; }
    public bool TryInspect(string name, out object? value);
}
