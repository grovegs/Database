namespace GroveGames.Database.Storage;

internal sealed class NoEntrySigner : IEntrySigner
{
    public static readonly NoEntrySigner Instance = new();

    private NoEntrySigner()
    {
    }

    public int TagLength => 0;

    public void Sign(ReadOnlySpan<byte> data, Span<byte> tag)
    {
    }

    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> tag)
    {
        return true;
    }

    public void Dispose()
    {
    }
}
