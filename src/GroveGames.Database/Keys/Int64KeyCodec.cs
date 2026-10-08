using System.Buffers.Binary;
using GroveGames.Serialization;

namespace GroveGames.Database.Keys;

internal sealed class Int64KeyCodec : IKeyCodec<long>
{
    public void Write(ByteBuffer output, long key)
    {
        BinaryPrimitives.WriteInt64LittleEndian(output.Take(8), key);
    }

    public long Read(ReadOnlySpan<byte> data, out int consumed)
    {
        consumed = 8;
        return data.Length >= 8 ? BinaryPrimitives.ReadInt64LittleEndian(data) : throw new FormatException("A key is truncated.");
    }
}
