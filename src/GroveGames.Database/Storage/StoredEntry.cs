namespace GroveGames.Database.Storage;

internal readonly struct StoredEntry
{
    private readonly EntryKind _kind;
    private readonly int _start;
    private readonly int _length;
    private readonly int _payloadStart;
    private readonly int _payloadLength;

    public EntryKind Kind => _kind;
    public int Start => _start;
    public int Length => _length;
    public int PayloadStart => _payloadStart;
    public int PayloadLength => _payloadLength;

    public StoredEntry(EntryKind kind, int start, int length, int payloadStart, int payloadLength)
    {
        _kind = kind;
        _start = start;
        _length = length;
        _payloadStart = payloadStart;
        _payloadLength = payloadLength;
    }
}
