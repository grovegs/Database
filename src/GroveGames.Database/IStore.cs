using System.Buffers;
using GroveGames.Serialization;

namespace GroveGames.Database;

internal interface IStore : IDisposable
{
    public bool IsCollection { get; }
    public object? Inspect();
    public void Write(object value);
    public void WriteItem(int index, object value);
    public void WriteJson(JsonSerializer serializer, IBufferWriter<byte> output);
    public void Sync();
}
