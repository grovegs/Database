namespace GroveGames.Database;

internal interface IStore : IDisposable
{
    public void Sync();
}
