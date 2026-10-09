namespace GroveGames.Database;

public sealed class DatabaseTamperedException : Exception
{
    public DatabaseTamperedException(string message)
        : base(message)
    {
    }
}
