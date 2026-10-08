using System.Collections;
using GroveGames.Database.Keys;
using GroveGames.Database.Storage;
using GroveGames.Serialization;

namespace GroveGames.Database;

public sealed class DocumentCollection<TKey, T> : IDocumentCollection<TKey, T>, IStore
    where TKey : notnull
{
    private const int CompactionThreshold = 16;

    private readonly StoreFile _file;
    private readonly MessagePackSerializer _serializer;
    private readonly Func<T, TKey> _keySelector;
    private readonly IKeyCodec<TKey> _keyCodec;
    private readonly Dictionary<TKey, T> _items;

    internal DocumentCollection(string path, MessagePackSerializer serializer, Func<T, TKey> keySelector)
    {
        _serializer = serializer;
        _keySelector = keySelector;
        _keyCodec = KeyCodecs.Create<TKey>();
        _items = [];
        _file = StoreFile.Open(path, StoreKind.Collection);

        try
        {
            Load();
        }
        catch
        {
            _file.Dispose();
            throw;
        }
    }

    public int Count => _items.Count;

    public bool Contains(TKey key)
    {
        return _items.ContainsKey(key);
    }

    public T Get(TKey key)
    {
        return _items.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"No document has the key '{key}'.");
    }

    public bool TryGet(TKey key, out T value)
    {
        return _items.TryGetValue(key, out value!);
    }

    public void Upsert(T value)
    {
        var key = _keySelector(value);
        var payload = _file.BeginEntry(EntryKind.Upsert);
        _keyCodec.Write(payload, key);
        _serializer.Serialize(value, payload);
        _file.CommitEntry();
        _items[key] = value;
    }

    public bool Remove(TKey key)
    {
        if (!_items.ContainsKey(key))
        {
            return false;
        }

        var payload = _file.BeginEntry(EntryKind.Remove);
        _keyCodec.Write(payload, key);
        _file.CommitEntry();
        _items.Remove(key);
        return true;
    }

    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _file.BeginEntry(EntryKind.Clear);
        _file.CommitEntry();
        _items.Clear();
    }

    public Dictionary<TKey, T>.ValueCollection.Enumerator GetEnumerator()
    {
        return _items.Values.GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    void IStore.Sync()
    {
        _file.Sync();
    }

    void IDisposable.Dispose()
    {
        _file.Dispose();
    }

    private void Load()
    {
        var contents = _file.Contents;
        var entries = _file.Entries;
        var latest = new Dictionary<TKey, int>();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            switch (entry.Kind)
            {
                case EntryKind.Upsert:
                    latest[_keyCodec.Read(contents.AsSpan(entry.PayloadStart, entry.PayloadLength), out _)] = i;
                    break;
                case EntryKind.Remove:
                    latest.Remove(_keyCodec.Read(contents.AsSpan(entry.PayloadStart, entry.PayloadLength), out _));
                    break;
                case EntryKind.Clear:
                    latest.Clear();
                    break;
                default:
                    throw new FormatException($"A collection cannot contain {entry.Kind} entries.");
            }
        }

        var live = new List<StoredEntry>(latest.Count);

        foreach (var pair in latest)
        {
            var entry = entries[pair.Value];
            _keyCodec.Read(contents.AsSpan(entry.PayloadStart, entry.PayloadLength), out var keyLength);
            var value = _serializer.Deserialize<T>(contents.AsMemory(entry.PayloadStart + keyLength, entry.PayloadLength - keyLength));
            _items[pair.Key] = value!;
            live.Add(entry);
        }

        var dead = entries.Count - live.Count;

        if (dead > CompactionThreshold && dead > live.Count)
        {
            live.Sort(static (left, right) => left.Start.CompareTo(right.Start));
            _file.Rewrite(live);
        }

        _file.ReleaseContents();
    }
}
