namespace GroveGames.Database;

public interface IDatabaseSettings
{
    public string FolderName { get; }
    public bool TamperProtection { get; }
}
