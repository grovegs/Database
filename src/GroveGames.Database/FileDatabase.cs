using GroveGames.Database.Storage;
using GroveGames.Serialization;

namespace GroveGames.Database;

public sealed class FileDatabase : IDatabase
{
    private const string Extension = ".db";

    private readonly string _directory;
    private readonly MessagePackSerializer _serializer;
    private readonly Dictionary<string, object> _stores;
    private bool _disposed;

    public FileDatabase(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            var parent = Path.GetDirectoryName(Path.GetFullPath(directory));

            if (parent is not null)
            {
                DiskSync.FlushDirectory(parent);
            }
        }

        _directory = directory;
        _serializer = new MessagePackSerializer();
        _stores = new Dictionary<string, object>(StringComparer.Ordinal);
        _disposed = false;
    }

    public IDocument<T> GetDocument<T>(string name) where T : new()
    {
        return GetStore(name, path => new Document<T>(path, _serializer));
    }

    public IDocumentCollection<TKey, T> GetDocumentCollection<TKey, T>(string name, Func<T, TKey> keySelector) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return GetStore(name, path => new DocumentCollection<TKey, T>(path, _serializer, keySelector));
    }

    public void Save()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (var store in _stores.Values)
        {
            ((IStore)store).Sync();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var store in _stores.Values)
        {
            ((IStore)store).Dispose();
        }

        _stores.Clear();
    }

    private TStore GetStore<TStore>(string name, Func<string, TStore> create) where TStore : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateName(name);

        if (_stores.TryGetValue(name, out var existing))
        {
            return existing as TStore ?? throw new InvalidOperationException($"'{name}' is already open as a {existing.GetType().Name}.");
        }

        var store = create(Path.Combine(_directory, name + Extension));
        _stores.Add(name, store);
        return store;
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        foreach (var character in name)
        {
            if (!char.IsLetterOrDigit(character) && character != '_' && character != '-' && character != '.')
            {
                throw new ArgumentException($"'{name}' may only contain letters, digits, '_', '-' and '.'.", nameof(name));
            }
        }
    }
}
