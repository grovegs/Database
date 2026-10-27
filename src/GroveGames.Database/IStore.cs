namespace GroveGames.Database;

internal interface IStore : IDisposable
{
    public bool IsCollection { get; }
    public object? Inspect();
    public void Write(object value);
    public void Sync();
}
