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
    private const byte ChainedVersion = 3;

    private static readonly byte[] s_magic = [(byte)'G', (byte)'G', (byte)'D', (byte)'B'];

    private readonly string _path;
    private readonly string _temporaryPath;
    private readonly StoreKind _kind;
    private readonly ByteBuffer _payload;
    private readonly IEntrySigner _signer;
    private readonly byte[] _chain;
    private readonly byte[] _rewriteChain;
    private byte[] _scratch;
    private EntryKind _pendingKind;
    private FileStream _stream;
    private FileStream? _rewrite;
    private bool _dirty;
    private bool _disposed;
    private byte[] _contents;
    private List<StoredEntry> _entries;

    private StoreFile(string path, StoreKind kind, FileStream stream, byte[] contents, List<StoredEntry> entries, IEntrySigner signer, byte[] chain)
    {
        _path = path;
        _temporaryPath = path + ".tmp";
        _kind = kind;
        _stream = stream;
        _signer = signer;
        _chain = chain;
        _rewriteChain = new byte[signer.TagLength];
        _payload = new ByteBuffer(256);
        _scratch = new byte[256];
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
        var temporary = path + ".tmp";

        if (File.Exists(temporary))
        {
            File.Delete(temporary);
        }

        var exists = File.Exists(path);
        var contents = exists ? File.ReadAllBytes(path) : [];
        var entries = new List<StoredEntry>();
        var chain = new byte[signer.TagLength];
        var validLength = Parse(path, contents, kind, entries, signer, chain, out var current);
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);

        try
        {
            if (validLength == 0)
            {
                stream.SetLength(0);
                stream.Write(CreateHeader(kind, signer.TagLength > 0), 0, HeaderLength);
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

            stream.Position = validLength;
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        var file = new StoreFile(path, kind, stream, contents, entries, signer, chain);

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
        var tagLength = _signer.TagLength;
        var length = EntryPrefixLength + payload.Length + ChecksumLength + tagLength;

        if (_scratch.Length < length)
        {
            _scratch = new byte[Math.Max(length, _scratch.Length * 2)];
        }

        var entry = _scratch.AsSpan(0, length);
        BinaryPrimitives.WriteInt32LittleEndian(entry, payload.Length);
        entry[4] = (byte)_pendingKind;
        payload.CopyTo(entry.Slice(EntryPrefixLength));
        var signed = entry.Slice(4, payload.Length + 1);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(EntryPrefixLength + payload.Length), Crc32.Compute(signed));
        var chain = _rewrite is null ? _chain : _rewriteChain;
        var tag = entry.Slice(EntryPrefixLength + payload.Length + ChecksumLength);
        _signer.Sign(chain, signed, tag);
        var stream = _rewrite ?? _stream;
        var start = stream.Position;

        try
        {
            stream.Write(_scratch, 0, length);
        }
        catch
        {
            stream.SetLength(start);
            stream.Position = start;
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
            rewrite.Write(CreateHeader(_kind, _signer.TagLength > 0), 0, HeaderLength);
            write(this, state);
            DiskSync.Flush(rewrite);
        }
        finally
        {
            _rewrite = null;
            rewrite.Dispose();
        }

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
            _stream = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);
            _stream.Position = _stream.Length;

            if (replaced)
            {
                _rewriteChain.CopyTo(_chain, 0);
                _dirty = false;
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

        Sync();
        _disposed = true;
        _rewrite?.Dispose();
        _stream.Dispose();
        _signer.Dispose();
    }

    private static int Parse(string path, byte[] contents, StoreKind kind, List<StoredEntry> entries, IEntrySigner signer, byte[] chain, out bool current)
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

            if (verify && !signer.Verify(version == ChainedVersion ? chain : [], signed, tag))
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

    private static byte[] CreateHeader(StoreKind kind, bool authenticated)
    {
        return [s_magic[0], s_magic[1], s_magic[2], s_magic[3], authenticated ? ChainedVersion : PlainVersion, (byte)kind, 0, 0];
    }
}
