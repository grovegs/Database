namespace GroveGames.Database;

internal interface IStore : IDisposable
{
    public object? Inspect();
    public void Sync();
}
