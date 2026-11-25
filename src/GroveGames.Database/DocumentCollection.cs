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
    private int _entryCount;

    internal DocumentCollection(string path, MessagePackSerializer serializer, Func<T, TKey> keySelector, IEntrySigner signer)
    {
        _serializer = serializer;
        _keySelector = keySelector;
        _keyCodec = KeyCodecs.Create<TKey>();
        _items = [];
        _file = StoreFile.Open(path, StoreKind.Collection, signer);

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

    bool IStore.IsCollection => true;

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
        WriteUpsert(key, value);
        _items[key] = value;
        _entryCount++;
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
        _entryCount++;
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
        _entryCount++;
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

    object? IStore.Inspect()
    {
        return _items.Values;
    }

    void IStore.Write(object value)
    {
        if (value is not T typed)
        {
            throw new ArgumentException($"The value must be a {typeof(T).Name}.", nameof(value));
        }

        var key = _keySelector(typed);

        foreach (var pair in _items)
        {
            if (ReferenceEquals(pair.Value, value) && !EqualityComparer<TKey>.Default.Equals(pair.Key, key))
            {
                Remove(pair.Key);
                break;
            }
        }

        Upsert(typed);
    }

    void IStore.Sync()
    {
        var dead = _entryCount - _items.Count;

        if (dead > CompactionThreshold && dead > _items.Count)
        {
            _file.Rewrite(this, static (_, collection) =>
            {
                foreach (var pair in collection._items)
                {
                    collection.WriteUpsert(pair.Key, pair.Value);
                }
            });

            _entryCount = _items.Count;
            return;
        }

        _file.Sync();
    }

    void IDisposable.Dispose()
    {
        _file.Dispose();
    }

    private void WriteUpsert(TKey key, T value)
    {
        var payload = _file.BeginEntry(EntryKind.Upsert);
        _keyCodec.Write(payload, key);
        _serializer.Serialize(value, payload);
        _file.CommitEntry();
    }

    private void Load()
    {
        var contents = _file.Contents;
        var entries = _file.Entries;
        var latest = new Dictionary<TKey, (int Index, int KeyLength)>();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            switch (entry.Kind)
            {
                case EntryKind.Upsert:
                    var key = _keyCodec.Read(contents.AsSpan(entry.PayloadStart, entry.PayloadLength), out var keyLength);
                    latest[key] = (i, keyLength);
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

        var version = Formatters.GetVersion<T>();
        var live = new List<(StoredEntry Entry, TKey Key, bool Outdated)>(latest.Count);
        var outdated = false;

        foreach (var pair in latest)
        {
            var entry = entries[pair.Value.Index];
            var keyLength = pair.Value.KeyLength;
            var payload = contents.AsMemory(entry.PayloadStart + keyLength, entry.PayloadLength - keyLength);
            _items[pair.Key] = _serializer.Deserialize<T>(payload)!;
            var isOutdated = _serializer.ReadVersion(payload) < version;
            live.Add((entry, pair.Key, isOutdated));
            outdated |= isOutdated;
        }

        var dead = entries.Count - live.Count;
        _entryCount = entries.Count;

        if (outdated || (dead > CompactionThreshold && dead > live.Count))
        {
            live.Sort(static (left, right) => left.Entry.Start.CompareTo(right.Entry.Start));
            _file.Rewrite((this, live), static (file, state) =>
            {
                foreach (var (entry, key, isOutdated) in state.live)
                {
                    if (isOutdated)
                    {
                        state.Item1.WriteUpsert(key, state.Item1._items[key]);
                    }
                    else
                    {
                        file.CopyEntry(entry);
                    }
                }
            });

            _entryCount = live.Count;
        }

        _file.ReleaseContents();
    }
}
