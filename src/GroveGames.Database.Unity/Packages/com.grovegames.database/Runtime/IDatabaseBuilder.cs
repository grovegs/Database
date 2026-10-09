namespace GroveGames.Database.Unity
{
    public interface IDatabaseBuilder
    {
        IDatabaseBuilder SetFolderName(string folderName);
        IDatabaseBuilder SetTamperProtection(bool enabled);
    }
}
