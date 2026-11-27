using System.Buffers.Binary;
using System.Text;
using GroveGames.Serialization;

namespace GroveGames.Database.Keys;

internal sealed class StringKeyCodec : IKeyCodec<string>
{
    public byte KeyType => 3;

    public void Write(ByteBuffer output, string key)
    {
        for (var i = 0; i < key.Length; i++)
        {
            if (char.IsHighSurrogate(key[i]) && i + 1 < key.Length && char.IsLowSurrogate(key[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(key[i]))
            {
                throw new ArgumentException("A string key contains a lone surrogate, which UTF-8 cannot store, so two keys could collide.", nameof(key));
            }
        }

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
