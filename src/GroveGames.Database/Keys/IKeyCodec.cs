using GroveGames.Serialization;

namespace GroveGames.Database.Keys;

internal interface IKeyCodec<TKey>
{
    public void Write(ByteBuffer output, TKey key);

    public TKey Read(ReadOnlySpan<byte> data, out int consumed);
}
