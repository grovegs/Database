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
    private int _entryCount;

    internal Document(string path, MessagePackSerializer serializer, IEntrySigner signer)
    {
        _serializer = serializer;
        _file = StoreFile.Open(path, StoreKind.Document, signer);

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

    bool IStore.IsCollection => false;

    public void Set(T value)
    {
        var payload = _file.BeginEntry(EntryKind.Value);
        _serializer.Serialize(value, payload);
        _file.CommitEntry();
        _value = value;
        _entryCount++;
    }

    object? IStore.Inspect()
    {
        return _value;
    }

    void IStore.Write(object value)
    {
        if (value is not T typed)
        {
            throw new ArgumentException($"The value must be a {typeof(T).Name}.", nameof(value));
        }

        Set(typed);
    }

    void IStore.Sync()
    {
        if (_entryCount > CompactionThreshold)
        {
            _file.BeginRewrite();
            _serializer.Serialize(_value, _file.BeginEntry(EntryKind.Value));
            _file.CommitEntry();
            _file.CommitRewrite();
            _entryCount = 1;
            return;
        }

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
            _entryCount = entries.Count;
            _file.ReleaseContents();
            return new T();
        }

        _entryCount = 1;

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
        else
        {
            _entryCount = entries.Count;
        }

        _file.ReleaseContents();
        return value;
    }
}
