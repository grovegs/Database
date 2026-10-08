using GroveGames.Database.Storage;
using GroveGames.Serialization;

namespace GroveGames.Database;

public sealed class Document<T> : IDocument<T>, IStore
    where T : new()
{
    private const int CompactionThreshold = 16;

    private readonly StoreFile _file;
    private readonly MessagePackSerializer _serializer;
    private T _value;

    internal Document(string path, MessagePackSerializer serializer)
    {
        _serializer = serializer;
        _file = StoreFile.Open(path, StoreKind.Document);

        try
        {
            _value = Load();
        }
        catch
        {
            _file.Dispose();
            throw;
        }
    }

    public T Value => _value;

    public void Set(T value)
    {
        var payload = _file.BeginEntry(EntryKind.Value);
        _serializer.Serialize(value, payload);
        _file.CommitEntry();
        _value = value;
    }

    void IStore.Sync()
    {
        _file.Sync();
    }

    void IDisposable.Dispose()
    {
        _file.Dispose();
    }

    private T Load()
    {
        var entries = _file.Entries;
        var latest = -1;

        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Kind == EntryKind.Value)
            {
                latest = i;
                break;
            }
        }

        if (latest < 0)
        {
            _file.ReleaseContents();
            return new T();
        }

        var payload = _file.Contents.AsMemory(entries[latest].PayloadStart, entries[latest].PayloadLength);
        var value = _serializer.Deserialize<T>(payload) ?? new T();

        if (PayloadVersion.Read(payload.Span) < Formatters.GetVersion<T>())
        {
            _file.BeginRewrite();
            _serializer.Serialize(value, _file.BeginEntry(EntryKind.Value));
            _file.CommitEntry();
            _file.CommitRewrite();
        }
        else if (entries.Count > CompactionThreshold)
        {
            _file.BeginRewrite();
            _file.CopyEntry(entries[latest]);
            _file.CommitRewrite();
        }

        _file.ReleaseContents();
        return value;
    }
}
