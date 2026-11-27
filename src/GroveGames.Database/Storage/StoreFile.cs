using System.Buffers.Binary;
using GroveGames.Serialization;

namespace GroveGames.Database.Storage;

internal sealed class StoreFile : IDisposable
{
    private const int HeaderLength = 8;
    private const int KeyTypeIndex = 6;
    private const int EntryPrefixLength = 5;
    private const int ChecksumLength = 4;
    private const byte PlainVersion = 1;
    private const byte AuthenticatedVersion = 2;
    private const byte ChainedVersion = 3;

    private static readonly byte[] s_magic = [(byte)'G', (byte)'G', (byte)'D', (byte)'B'];

    private readonly string _path;
    private readonly string _temporaryPath;
    private readonly StoreKind _kind;
    private readonly byte _keyType;
    private readonly ByteBuffer _payload;
    private readonly IEntrySigner _signer;
    private readonly byte[] _chain;
    private readonly byte[] _rewriteChain;
    private EntryKind _pendingKind;
    private FileStream _stream;
    private FileStream? _rewrite;
    private bool _dirty;
    private bool _needsReopen;
    private bool _disposed;
    private byte[] _contents;
    private List<StoredEntry> _entries;

    private StoreFile(string path, StoreKind kind, byte keyType, FileStream stream, byte[] contents, List<StoredEntry> entries, IEntrySigner signer, byte[] chain)
    {
        _path = path;
        _temporaryPath = path + ".tmp";
        _kind = kind;
        _keyType = keyType;
        _stream = stream;
        _signer = signer;
        _chain = chain;
        _rewriteChain = new byte[signer.TagLength];
        _payload = new ByteBuffer(256);
        _pendingKind = EntryKind.Value;
        _rewrite = null;
        _dirty = false;
        _disposed = false;
        _contents = contents;
        _entries = entries;
    }

    public byte[] Contents => _contents;

    public List<StoredEntry> Entries => _entries;

    public static StoreFile Open(string path, StoreKind kind, IEntrySigner signer)
    {
        return Open(path, kind, 0, signer);
    }

    public static StoreFile Open(string path, StoreKind kind, byte keyType, IEntrySigner signer)
    {
        var temporary = path + ".tmp";

        if (File.Exists(temporary))
        {
            File.Delete(temporary);
        }

        var exists = File.Exists(path);
        var contents = exists ? File.ReadAllBytes(path) : [];
        var entries = new List<StoredEntry>();
        var chain = new byte[signer.TagLength];
        var validLength = Parse(path, contents, kind, keyType, entries, signer, chain, out var current);
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);

        try
        {
            if (validLength == 0)
            {
                stream.SetLength(0);
                stream.Write(CreateHeader(kind, keyType, signer.TagLength > 0), 0, HeaderLength);
                DiskSync.Flush(stream);
                validLength = HeaderLength;
                current = true;

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

            if (keyType != 0 && contents.Length >= HeaderLength && contents[KeyTypeIndex] == 0)
            {
                stream.Position = KeyTypeIndex;
                stream.WriteByte(keyType);
            }

            stream.Position = validLength;
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        var file = new StoreFile(path, kind, keyType, stream, contents, entries, signer, chain);

        if (current)
        {
            return file;
        }

        try
        {
            file.Rewrite(entries, static (target, copied) =>
            {
                foreach (var entry in copied)
                {
                    target.CopyEntry(entry);
                }
            });
        }
        catch
        {
            file.Dispose();
            throw;
        }

        return file;
    }

    public static void Resign(string path, IEntrySigner signer)
    {
        var header = new byte[HeaderLength];

        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Read(header, 0, HeaderLength) < HeaderLength || !header.AsSpan(0, 4).SequenceEqual(s_magic))
            {
                return;
            }
        }

        Open(path, (StoreKind)header[5], header[KeyTypeIndex], signer).Dispose();
    }

    public ByteBuffer BeginEntry(EntryKind kind)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _payload.Reset();
        _payload.Take(EntryPrefixLength);
        _pendingKind = kind;
        return _payload;
    }

    public void CommitEntry()
    {
        var payloadLength = _payload.Length - EntryPrefixLength;
        var tagLength = _signer.TagLength;
        _payload.Take(ChecksumLength + tagLength);
        var entry = _payload.Slice(0, _payload.Length);
        BinaryPrimitives.WriteInt32LittleEndian(entry, payloadLength);
        entry[4] = (byte)_pendingKind;
        var signed = entry.Slice(4, payloadLength + 1);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(EntryPrefixLength + payloadLength), Crc32.Compute(signed));
        var chain = _rewrite is null ? _chain : _rewriteChain;
        var tag = entry.Slice(EntryPrefixLength + payloadLength + ChecksumLength, tagLength);
        _signer.Sign(chain, signed, tag);
        if (_rewrite is null && _needsReopen)
        {
            Reopen();
        }

        var stream = _rewrite ?? _stream;
        var start = stream.Position;

        try
        {
            _payload.WriteTo(stream);
        }
        catch
        {
            RollBack(stream, start);
            throw;
        }

        tag.CopyTo(chain);
        _dirty = _rewrite is null || _dirty;
    }

    public void CopyEntry(StoredEntry entry)
    {
        var rewrite = _rewrite!;
        rewrite.Write(_contents, entry.Start, EntryPrefixLength + entry.PayloadLength + ChecksumLength);

        if (_signer.TagLength == 0)
        {
            return;
        }

        Span<byte> tag = stackalloc byte[_signer.TagLength];
        _signer.Sign(_rewriteChain, _contents.AsSpan(entry.PayloadStart - 1, entry.PayloadLength + 1), tag);
        rewrite.Write(tag);
        tag.CopyTo(_rewriteChain);
    }

    public void Sync()
    {
        if (_disposed || !_dirty)
        {
            return;
        }

        if (_needsReopen)
        {
            Reopen();
        }

        DiskSync.Flush(_stream);
        _dirty = false;
    }

    public void Rewrite<TState>(TState state, Action<StoreFile, TState> write)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var rewrite = new FileStream(_temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.None);
        _rewrite = rewrite;
        _rewriteChain.AsSpan().Clear();

        try
        {
            rewrite.Write(CreateHeader(_kind, _keyType, _signer.TagLength > 0), 0, HeaderLength);
            write(this, state);
            DiskSync.Flush(rewrite);
        }
        catch
        {
            _rewrite = null;
            rewrite.Dispose();
            DeleteTemporary();
            throw;
        }

        _rewrite = null;
        rewrite.Dispose();

        _stream.Dispose();
        var replaced = false;

        try
        {
            File.Replace(_temporaryPath, _path, null);
            replaced = true;
            DiskSync.FlushDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        }
        finally
        {
            if (replaced)
            {
                _rewriteChain.CopyTo(_chain, 0);
                _dirty = false;
            }

            try
            {
                Reopen();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _needsReopen = true;
            }
        }
    }

    public void ReleaseContents()
    {
        _contents = [];
        _entries = [];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Sync();
        }
        finally
        {
            _disposed = true;
            _rewrite?.Dispose();
            _stream.Dispose();
            _signer.Dispose();
        }
    }

    private void Reopen()
    {
        _stream = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);
        _stream.Position = _stream.Length;
        _needsReopen = false;
    }

    // When the truncation fails too, the next entry still starts where this one did, so the partial bytes are overwritten.
    private static void RollBack(FileStream stream, long start)
    {
        try
        {
            stream.SetLength(start);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        try
        {
            stream.Position = start;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void DeleteTemporary()
    {
        try
        {
            File.Delete(_temporaryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static int Parse(string path, byte[] contents, StoreKind kind, byte keyType, List<StoredEntry> entries, IEntrySigner signer, byte[] chain, out bool current)
    {
        current = true;

        if (contents.Length < HeaderLength)
        {
            return 0;
        }

        var version = contents[4];

        if (!contents.AsSpan(0, 4).SequenceEqual(s_magic) || version is not (PlainVersion or AuthenticatedVersion or ChainedVersion))
        {
            throw new FormatException($"'{path}' is not a database file of a supported version.");
        }

        var authenticated = version != PlainVersion;

        if (authenticated && signer.TagLength == 0)
        {
            throw new InvalidOperationException($"'{path}' is protected, so the database must be opened with its key.");
        }

        if (contents[5] != (byte)kind)
        {
            throw new FormatException($"'{path}' stores a {(StoreKind)contents[5]}, not a {kind}.");
        }

        if (keyType != 0 && contents[KeyTypeIndex] != 0 && contents[KeyTypeIndex] != keyType)
        {
            throw new FormatException($"'{path}' stores keys of another type, so the collection's key type was changed.");
        }

        var tagLength = authenticated ? signer.TagLength : 0;
        var verify = authenticated && !signer.TrustsExistingTags;
        var position = HeaderLength;

        while (contents.Length - position >= EntryPrefixLength + ChecksumLength + tagLength)
        {
            var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(contents.AsSpan(position));

            if (payloadLength < 0 || payloadLength > contents.Length - position - EntryPrefixLength - ChecksumLength - tagLength)
            {
                break;
            }

            var signed = contents.AsSpan(position + 4, payloadLength + 1);
            var checksumStart = position + EntryPrefixLength + payloadLength;

            if (Crc32.Compute(signed) != BinaryPrimitives.ReadUInt32LittleEndian(contents.AsSpan(checksumStart)))
            {
                break;
            }

            var tag = contents.AsSpan(checksumStart + ChecksumLength, tagLength);

            if (verify && !(version == ChainedVersion ? signer.Verify(chain, signed, tag) : signer.VerifyUnchained(signed, tag)))
            {
                throw new DatabaseTamperedException($"'{path}' was modified outside the database.");
            }

            if (version == ChainedVersion)
            {
                tag.CopyTo(chain);
            }

            var length = EntryPrefixLength + payloadLength + ChecksumLength + tagLength;
            entries.Add(new StoredEntry((EntryKind)contents[position + 4], position, length, position + EntryPrefixLength, payloadLength));
            position += length;
        }

        if (signer.TagLength > 0 && !authenticated && entries.Count > 0 && !signer.TrustsExistingTags)
        {
            throw new DatabaseTamperedException($"'{path}' is not protected, so it was not written by this database with its key.");
        }

        current = signer.TagLength == 0 || (version == ChainedVersion && !signer.TrustsExistingTags);
        return position;
    }

    private static byte[] CreateHeader(StoreKind kind, byte keyType, bool authenticated)
    {
        return [s_magic[0], s_magic[1], s_magic[2], s_magic[3], authenticated ? ChainedVersion : PlainVersion, (byte)kind, keyType, 0];
    }
}
