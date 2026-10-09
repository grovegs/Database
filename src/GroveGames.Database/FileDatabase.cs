using GroveGames.Database.Storage;
using GroveGames.Serialization;

namespace GroveGames.Database;

public sealed class FileDatabase : IDatabase
{
    private const string Extension = ".db";
    private const int MinimumKeyLength = 16;

    private readonly string _directory;
    private readonly MessagePackSerializer _serializer;
    private readonly byte[]? _key;
    private readonly Dictionary<string, object> _stores;
    private bool _disposed;

    public FileDatabase(string directory)
        : this(directory, null)
    {
    }

    public FileDatabase(string directory, byte[]? key)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);

        if (key is not null && key.Length < MinimumKeyLength)
        {
            throw new ArgumentException($"The key must be at least {MinimumKeyLength} bytes.", nameof(key));
        }

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
        _key = key is null ? null : (byte[])key.Clone();
        _stores = new Dictionary<string, object>(StringComparer.Ordinal);
        _disposed = false;
    }

    public IDocument<T> GetDocument<T>(string name) where T : new()
    {
        return GetStore(name, (path, authenticator) => new Document<T>(path, _serializer, authenticator));
    }

    public IDocumentCollection<TKey, T> GetDocumentCollection<TKey, T>(string name, Func<T, TKey> keySelector) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return GetStore(name, (path, authenticator) => new DocumentCollection<TKey, T>(path, _serializer, keySelector, authenticator));
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

    private TStore GetStore<TStore>(string name, Func<string, EntryAuthenticator?, TStore> create) where TStore : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateName(name);

        if (_stores.TryGetValue(name, out var existing))
        {
            return existing as TStore ?? throw new InvalidOperationException($"'{name}' is already open as a {existing.GetType().Name}.");
        }

        var authenticator = _key is null ? null : new EntryAuthenticator(_key, name);
        TStore store;

        try
        {
            store = create(Path.Combine(_directory, name + Extension), authenticator);
        }
        catch
        {
            authenticator?.Dispose();
            throw;
        }

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
