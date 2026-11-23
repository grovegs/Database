namespace GroveGames.Database;

public sealed class DatabaseSettings : IDatabaseSettings
{
    private readonly string _folderName;
    private readonly bool _tamperProtection;

    public string FolderName => _folderName;
    public bool TamperProtection => _tamperProtection;

    public DatabaseSettings(string folderName, bool tamperProtection)
    {
        ArgumentException.ThrowIfNullOrEmpty(folderName);

        _folderName = folderName;
        _tamperProtection = tamperProtection;
    }
}
