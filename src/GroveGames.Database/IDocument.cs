namespace GroveGames.Database;

public interface IDocument<T>
{
    public T Value { get; }
    public void Set(T value);
}
