using GroveGames.Serialization;

namespace GroveGames.Database.Keys;

internal sealed class GuidKeyCodec : IKeyCodec<Guid>
{
    public void Write(ByteBuffer output, Guid key)
    {
        key.TryWriteBytes(output.Take(16));
    }

    public Guid Read(ReadOnlySpan<byte> data, out int consumed)
    {
        consumed = 16;
        return data.Length >= 16 ? new Guid(data.Slice(0, 16)) : throw new FormatException("A key is truncated.");
    }
}
