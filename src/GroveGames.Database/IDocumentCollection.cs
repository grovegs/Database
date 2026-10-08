namespace GroveGames.Database;

public interface IDocumentCollection<TKey, T> : IReadOnlyCollection<T>
    where TKey : notnull
{
    public bool Contains(TKey key);
    public T Get(TKey key);
    public bool TryGet(TKey key, out T value);
    public void Upsert(T value);
    public bool Remove(TKey key);
    public void Clear();
}
