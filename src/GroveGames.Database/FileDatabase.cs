using System.Buffers;
using System.Runtime.ExceptionServices;
using GroveGames.Database.Storage;
using GroveGames.Serialization;

namespace GroveGames.Database;

public sealed class FileDatabase : IDatabase, IDatabaseInspector
{
    private const string Extension = ".db";

    private readonly string _directory;
    private readonly MessagePackSerializer _serializer;
    private readonly JsonSerializer _json;
    private DatabaseProtection _protection;
    private readonly Dictionary<string, IStore> _stores;
    private readonly Action<string, Exception>? _unreadableStoreHandler;
    private bool _disposed;
    private bool _areExistingStoresSigned;

    public FileDatabase(string directory)
        : this(directory, DatabaseProtection.None)
    {
    }

    public FileDatabase(string directory, DatabaseProtection protection)
        : this(directory, protection, null)
    {
    }

    public FileDatabase(string directory, DatabaseProtection protection, Action<string, Exception>? unreadableStoreHandler)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentNullException.ThrowIfNull(protection);

        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
            var parent = Path.GetDirectoryName(Path.GetFullPath(directory));

            if (parent is not null)
            {
                DiskSync.FlushDirectory(parent);
            }
        }

        _directory = directory;
        _serializer = new MessagePackSerializer();
        _json = new JsonSerializer();
        _protection = protection;
        _stores = new Dictionary<string, IStore>(StringComparer.Ordinal);
        _unreadableStoreHandler = unreadableStoreHandler;
        _disposed = false;

        _areExistingStoresSigned = true;

        if (protection.TrustsExistingFiles)
        {
            _areExistingStoresSigned = ResignExistingStores();

            if (_areExistingStoresSigned)
            {
                _protection = protection.WithoutTrust();
            }
        }
    }

    public string Directory => _directory;

    internal bool AreExistingStoresSigned => _areExistingStoresSigned;
    public IReadOnlyCollection<string> OpenStoreNames => _stores.Keys;

    public IDocument<T> GetDocument<T>(string name) where T : new()
    {
        return GetStore(name, _serializer, static (path, signer, serializer) => new Document<T>(path, serializer, signer));
    }

    public IDocumentCollection<TKey, T> GetDocumentCollection<TKey, T>(string name, Func<T, TKey> keySelector) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return GetStore(name, (_serializer, keySelector), static (path, signer, state) => new DocumentCollection<TKey, T>(path, state._serializer, state.keySelector, signer));
    }

    public bool IsCollection(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _stores.TryGetValue(name, out var store) && store.IsCollection;
    }

    public bool TryInspect(string name, out object? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stores.TryGetValue(name, out var store))
        {
            value = store.Inspect();
            return true;
        }

        value = null;
        return false;
    }

    public bool TryWrite(string name, object value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(value);

        if (!_stores.TryGetValue(name, out var store))
        {
            return false;
        }

        store.Write(value);
        return true;
    }

    public bool TryWriteItem(string name, int index, object value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(value);

        if (!_stores.TryGetValue(name, out var store))
        {
            return false;
        }

        store.WriteItem(index, value);
        return true;
    }

    public bool TryWriteJson(string name, IBufferWriter<byte> output)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(output);

        if (!_stores.TryGetValue(name, out var store))
        {
            return false;
        }

        store.WriteJson(_json, output);
        return true;
    }

    public void Save()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Exception? first = null;

        foreach (var store in _stores.Values)
        {
            try
            {
                store.Sync();
            }
            catch (Exception exception)
            {
                first ??= exception;
            }
        }

        if (first is not null)
        {
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Exception? first = null;

        foreach (var store in _stores.Values)
        {
            try
            {
                store.Dispose();
            }
            catch (Exception exception)
            {
                first ??= exception;
            }
        }

        _stores.Clear();

        if (first is not null)
        {
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }

    private TStore GetStore<TStore, TState>(string name, TState state, Func<string, IEntrySigner, TState, TStore> create) where TStore : class, IStore
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stores.TryGetValue(name, out var existing))
        {
            return existing as TStore ?? throw new InvalidOperationException($"'{name}' is already open as a {existing.GetType().Name}.");
        }

        ValidateName(name);

        foreach (var open in _stores.Keys)
        {
            if (string.Equals(open, name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"'{name}' differs from the open store '{open}' only in case, and some file systems would give both the same file.");
            }
        }

        var path = Path.Combine(_directory, name + Extension);
        TStore store;

        try
        {
            store = Create(name, path, state, create);
        }
        catch (Exception exception) when (_unreadableStoreHandler is not null && exception is not (IOException or UnauthorizedAccessException))
        {
            Quarantine(path);
            _unreadableStoreHandler(name, exception);
            store = Create(name, path, state, create);
        }

        _stores.Add(name, store);
        return store;
    }

    private TStore Create<TStore, TState>(string name, string path, TState state, Func<string, IEntrySigner, TState, TStore> create) where TStore : class, IStore
    {
        var signer = _protection.CreateSigner(name);

        try
        {
            return create(path, signer, state);
        }
        catch
        {
            signer.Dispose();
            throw;
        }
    }

    // A store left unopened while existing files are trusted would keep tags from the old key and fail its check later.
    private bool ResignExistingStores()
    {
        var areSigned = true;

        foreach (var path in System.IO.Directory.GetFiles(_directory, "*" + Extension))
        {
            var signer = _protection.CreateSigner(Path.GetFileNameWithoutExtension(path));

            try
            {
                StoreFile.Resign(path, signer);
            }
            catch (FormatException)
            {
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                areSigned = false;
            }
            finally
            {
                signer.Dispose();
            }
        }

        return areSigned;
    }

    // Every quarantined copy is kept, because a store that fails to open may hold data a later build can read.
    private static void Quarantine(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var quarantined = path + ".corrupt";

        for (var number = 1; File.Exists(quarantined); number++)
        {
            quarantined = path + ".corrupt." + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        File.Move(path, quarantined);
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        foreach (var character in name)
        {
            if (!IsAsciiLetterOrDigit(character) && character != '_' && character != '-' && character != '.')
            {
                throw new ArgumentException($"'{name}' may only contain ASCII letters, digits, '_', '-' and '.'.", nameof(name));
            }
        }
    }
}
