using System.Buffers.Binary;
using System.Text;
using GroveGames.Serialization;

namespace GroveGames.Database.Keys;

internal sealed class StringKeyCodec : IKeyCodec<string>
{
    public void Write(ByteBuffer output, string key)
    {
        var length = Encoding.UTF8.GetByteCount(key);
        BinaryPrimitives.WriteInt32LittleEndian(output.Take(4), length);
        Encoding.UTF8.GetBytes(key.AsSpan(), output.Take(length));
    }

    public string Read(ReadOnlySpan<byte> data, out int consumed)
    {
        if (data.Length < 4)
        {
            throw new FormatException("A key is truncated.");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(data);

        if (length < 0 || length > data.Length - 4)
        {
            throw new FormatException("A key is truncated.");
        }

        consumed = 4 + length;
        return Encoding.UTF8.GetString(data.Slice(4, length));
    }
}
