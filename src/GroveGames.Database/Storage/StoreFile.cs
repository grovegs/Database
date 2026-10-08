using System.Buffers.Binary;
using GroveGames.Serialization;

namespace GroveGames.Database.Storage;

internal sealed class StoreFile : IDisposable
{
    private const int HeaderLength = 8;
    private const int EntryPrefixLength = 5;
    private const int ChecksumLength = 4;
    private const byte FormatVersion = 1;

    private static readonly byte[] s_magic = [(byte)'G', (byte)'G', (byte)'D', (byte)'B'];

    private readonly string _path;
    private readonly StoreKind _kind;
    private readonly ByteBuffer _payload;
    private byte[] _scratch;
    private EntryKind _pendingKind;
    private FileStream _stream;
    private bool _dirty;
    private bool _disposed;

    private StoreFile(string path, StoreKind kind, FileStream stream, byte[] contents, List<StoredEntry> entries)
    {
        _path = path;
        _kind = kind;
        _stream = stream;
        _payload = new ByteBuffer(256);
        _scratch = new byte[256];
        _pendingKind = EntryKind.Value;
        _dirty = false;
        _disposed = false;
        Contents = contents;
        Entries = entries;
    }

    public byte[] Contents { get; private set; }

    public List<StoredEntry> Entries { get; private set; }

    public static StoreFile Open(string path, StoreKind kind)
    {
        var temporary = path + ".tmp";

        if (File.Exists(temporary))
        {
            File.Delete(temporary);
        }

        var contents = File.Exists(path) ? File.ReadAllBytes(path) : [];
        var entries = new List<StoredEntry>();
        var validLength = Parse(path, contents, kind, entries);
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);

        try
        {
            if (validLength == 0)
            {
                stream.SetLength(0);
                stream.Write(CreateHeader(kind), 0, HeaderLength);
                DiskSync.Flush(stream);
                validLength = HeaderLength;
            }
            else if (validLength < contents.Length)
            {
                stream.SetLength(validLength);
                DiskSync.Flush(stream);
            }

            stream.Position = validLength;
            return new StoreFile(path, kind, stream, contents, entries);
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
        var length = EntryPrefixLength + payload.Length + ChecksumLength;

        if (_scratch.Length < length)
        {
            _scratch = new byte[Math.Max(length, _scratch.Length * 2)];
        }

        var entry = _scratch.AsSpan(0, length);
        BinaryPrimitives.WriteInt32LittleEndian(entry, payload.Length);
        entry[4] = (byte)_pendingKind;
        payload.CopyTo(entry.Slice(EntryPrefixLength));
        BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(EntryPrefixLength + payload.Length), Crc32.Compute(entry.Slice(4, payload.Length + 1)));
        _stream.Write(_scratch, 0, length);
        _dirty = true;
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

    public void Rewrite(IReadOnlyList<StoredEntry> live)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var temporary = _path + ".tmp";

        using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
        {
            output.Write(CreateHeader(_kind), 0, HeaderLength);

            for (var i = 0; i < live.Count; i++)
            {
                output.Write(Contents, live[i].Start, live[i].Length);
            }

            DiskSync.Flush(output);
        }

        _stream.Dispose();
        File.Replace(temporary, _path, null);
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
        _stream.Dispose();
    }

    private static int Parse(string path, byte[] contents, StoreKind kind, List<StoredEntry> entries)
    {
        if (contents.Length < HeaderLength)
        {
            return 0;
        }

        if (!contents.AsSpan(0, 4).SequenceEqual(s_magic) || contents[4] != FormatVersion)
        {
            throw new FormatException($"'{path}' is not a database file of a supported version.");
        }

        if (contents[5] != (byte)kind)
        {
            throw new FormatException($"'{path}' stores a {(StoreKind)contents[5]}, not a {kind}.");
        }

        var position = HeaderLength;

        while (contents.Length - position >= EntryPrefixLength + ChecksumLength)
        {
            var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(contents.AsSpan(position));

            if (payloadLength < 0 || payloadLength > contents.Length - position - EntryPrefixLength - ChecksumLength)
            {
                break;
            }

            var checksumStart = position + EntryPrefixLength + payloadLength;
            var expected = BinaryPrimitives.ReadUInt32LittleEndian(contents.AsSpan(checksumStart));

            if (Crc32.Compute(contents.AsSpan(position + 4, payloadLength + 1)) != expected)
            {
                break;
            }

            var entryKind = (EntryKind)contents[position + 4];
            var length = EntryPrefixLength + payloadLength + ChecksumLength;
            entries.Add(new StoredEntry(entryKind, position, length, position + EntryPrefixLength, payloadLength));
            position += length;
        }

        return position;
    }

    private static byte[] CreateHeader(StoreKind kind)
    {
        return [s_magic[0], s_magic[1], s_magic[2], s_magic[3], FormatVersion, (byte)kind, 0, 0];
    }
}
