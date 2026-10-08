namespace GroveGames.Database.Storage;

internal readonly struct StoredEntry
{
    public readonly EntryKind Kind;
    public readonly int Start;
    public readonly int Length;
    public readonly int PayloadStart;
    public readonly int PayloadLength;

    public StoredEntry(EntryKind kind, int start, int length, int payloadStart, int payloadLength)
    {
        Kind = kind;
        Start = start;
        Length = length;
        PayloadStart = payloadStart;
        PayloadLength = payloadLength;
    }
}
