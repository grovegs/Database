namespace GroveGames.Database;

public interface IDatabase : IDisposable
{
    public IDocument<T> GetDocument<T>(string name) where T : new();
    public IDocumentCollection<TKey, T> GetDocumentCollection<TKey, T>(string name, Func<T, TKey> keySelector) where TKey : notnull;
    public void Save();
}
