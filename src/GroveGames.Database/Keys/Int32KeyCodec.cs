using System.Buffers.Binary;
using GroveGames.Serialization;

namespace GroveGames.Database.Keys;

internal sealed class Int32KeyCodec : IKeyCodec<int>
{
    public void Write(ByteBuffer output, int key)
    {
        BinaryPrimitives.WriteInt32LittleEndian(output.Take(4), key);
    }

    public int Read(ReadOnlySpan<byte> data, out int consumed)
    {
        consumed = 4;
        return data.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(data) : throw new FormatException("A key is truncated.");
    }
}
