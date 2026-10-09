using System.Buffers.Binary;
using GroveGames.Serialization;

namespace GroveGames.Database.Storage;

internal sealed class StoreFile : IDisposable
{
    private const int HeaderLength = 8;
    private const int EntryPrefixLength = 5;
    private const int ChecksumLength = 4;
    private const byte PlainVersion = 1;
    private const byte AuthenticatedVersion = 2;

    private static readonly byte[] s_magic = [(byte)'G', (byte)'G', (byte)'D', (byte)'B'];

    private readonly string _path;
    private readonly StoreKind _kind;
    private readonly ByteBuffer _payload;
    private readonly EntryAuthenticator? _authenticator;
    private readonly bool _contentsAuthenticated;
    private byte[] _scratch;
    private EntryKind _pendingKind;
    private FileStream _stream;
    private FileStream? _rewrite;
    private bool _dirty;
    private bool _disposed;

    private StoreFile(string path, StoreKind kind, FileStream stream, byte[] contents, List<StoredEntry> entries, EntryAuthenticator? authenticator, bool contentsAuthenticated)
    {
        _path = path;
        _kind = kind;
        _stream = stream;
        _authenticator = authenticator;
        _contentsAuthenticated = contentsAuthenticated;
        _payload = new ByteBuffer(256);
        _scratch = new byte[256];
        _pendingKind = EntryKind.Value;
        _rewrite = null;
        _dirty = false;
        _disposed = false;
        Contents = contents;
        Entries = entries;
    }

    public byte[] Contents { get; private set; }

    public List<StoredEntry> Entries { get; private set; }

    public static StoreFile Open(string path, StoreKind kind, EntryAuthenticator? authenticator)
    {
        var temporary = path + ".tmp";

        if (File.Exists(temporary))
        {
            File.Delete(temporary);
        }

        var exists = File.Exists(path);
        var contents = exists ? File.ReadAllBytes(path) : [];
        var entries = new List<StoredEntry>();
        var validLength = Parse(path, contents, kind, entries, authenticator, out var authenticated);
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);

        try
        {
            if (validLength == 0)
            {
                stream.SetLength(0);
                stream.Write(CreateHeader(kind, authenticator != null), 0, HeaderLength);
                DiskSync.Flush(stream);
                validLength = HeaderLength;
                authenticated = authenticator != null;

                if (!exists)
                {
                    DiskSync.FlushDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                }
            }
            else if (validLength < contents.Length)
            {
                stream.SetLength(validLength);
                DiskSync.Flush(stream);
            }

            stream.Position = validLength;
            var file = new StoreFile(path, kind, stream, contents, entries, authenticator, authenticated);

            if (authenticator != null && !authenticated)
            {
                file.BeginRewrite();

                foreach (var entry in entries)
                {
                    file.CopyEntry(entry);
                }

                file.CommitRewrite();
            }

            return file;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public ByteBuffer BeginEntry(EntryKind kind)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _payload.Reset();
        _pendingKind = kind;
        return _payload;
    }

    public void CommitEntry()
    {
        var payload = _payload.WrittenSpan;
        var tagLength = _authenticator == null ? 0 : EntryAuthenticator.TagLength;
        var length = EntryPrefixLength + payload.Length + ChecksumLength + tagLength;

        if (_scratch.Length < length)
        {
            _scratch = new byte[Math.Max(length, _scratch.Length * 2)];
        }

        var entry = _scratch.AsSpan(0, length);
        BinaryPrimitives.WriteInt32LittleEndian(entry, payload.Length);
        entry[4] = (byte)_pendingKind;
        payload.CopyTo(entry.Slice(EntryPrefixLength));
        BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(EntryPrefixLength + payload.Length), Crc32.Compute(entry.Slice(4, payload.Length + 1)));
        _authenticator?.Compute(entry.Slice(4, payload.Length + 1), entry.Slice(EntryPrefixLength + payload.Length + ChecksumLength));
        (_rewrite ?? _stream).Write(_scratch, 0, length);
        _dirty = _rewrite is null || _dirty;
    }

    public void Sync()
    {
        if (_disposed || !_dirty)
        {
            return;
        }

        DiskSync.Flush(_stream);
        _dirty = false;
    }

    public void BeginRewrite()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _rewrite = new FileStream(_path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.None);
        _rewrite.Write(CreateHeader(_kind, _authenticator != null), 0, HeaderLength);
    }

    public void CopyEntry(StoredEntry entry)
    {
        _rewrite!.Write(Contents, entry.Start, entry.Length);

        if (_authenticator != null && !_contentsAuthenticated)
        {
            Span<byte> tag = stackalloc byte[EntryAuthenticator.TagLength];
            _authenticator.Compute(Contents.AsSpan(entry.PayloadStart - 1, entry.PayloadLength + 1), tag);
            _rewrite.Write(tag);
        }
    }

    public void CommitRewrite()
    {
        var rewrite = _rewrite!;
        DiskSync.Flush(rewrite);
        rewrite.Dispose();
        _rewrite = null;
        _stream.Dispose();
        File.Replace(_path + ".tmp", _path, null);
        DiskSync.FlushDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        _stream = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);
        _stream.Position = _stream.Length;
        _dirty = false;
    }

    public void ReleaseContents()
    {
        Contents = [];
        Entries = [];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Sync();
        _disposed = true;
        _rewrite?.Dispose();
        _stream.Dispose();
        _authenticator?.Dispose();
    }

    private static int Parse(string path, byte[] contents, StoreKind kind, List<StoredEntry> entries, EntryAuthenticator? authenticator, out bool authenticated)
    {
        authenticated = false;

        if (contents.Length < HeaderLength)
        {
            return 0;
        }

        if (!contents.AsSpan(0, 4).SequenceEqual(s_magic) || contents[4] is not (PlainVersion or AuthenticatedVersion))
        {
            throw new FormatException($"'{path}' is not a database file of a supported version.");
        }

        authenticated = contents[4] == AuthenticatedVersion;

        if (authenticated && authenticator == null)
        {
            throw new InvalidOperationException($"'{path}' is protected, so the database must be opened with its key.");
        }

        if (contents[5] != (byte)kind)
        {
            throw new FormatException($"'{path}' stores a {(StoreKind)contents[5]}, not a {kind}.");
        }

        var tagLength = authenticated ? EntryAuthenticator.TagLength : 0;
        var position = HeaderLength;

        while (contents.Length - position >= EntryPrefixLength + ChecksumLength + tagLength)
        {
            var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(contents.AsSpan(position));

            if (payloadLength < 0 || payloadLength > contents.Length - position - EntryPrefixLength - ChecksumLength - tagLength)
            {
                break;
            }

            var checksumStart = position + EntryPrefixLength + payloadLength;
            var expected = BinaryPrimitives.ReadUInt32LittleEndian(contents.AsSpan(checksumStart));

            if (Crc32.Compute(contents.AsSpan(position + 4, payloadLength + 1)) != expected)
            {
                break;
            }

            if (authenticated && !authenticator!.Verify(contents.AsSpan(position + 4, payloadLength + 1), contents.AsSpan(checksumStart + ChecksumLength, tagLength)))
            {
                throw new DatabaseTamperedException($"'{path}' was modified outside the database.");
            }

            var entryKind = (EntryKind)contents[position + 4];
            var length = EntryPrefixLength + payloadLength + ChecksumLength + tagLength;
            entries.Add(new StoredEntry(entryKind, position, length, position + EntryPrefixLength, payloadLength));
            position += length;
        }

        return position;
    }

    private static byte[] CreateHeader(StoreKind kind, bool authenticated)
    {
        return [s_magic[0], s_magic[1], s_magic[2], s_magic[3], authenticated ? AuthenticatedVersion : PlainVersion, (byte)kind, 0, 0];
    }
}
