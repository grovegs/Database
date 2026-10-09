namespace GroveGames.Database.Storage;

internal interface IEntrySigner : IDisposable
{
    public int TagLength { get; }

    public bool TrustsExistingTags { get; }

    public void Sign(ReadOnlySpan<byte> data, Span<byte> tag);

    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> tag);
}
