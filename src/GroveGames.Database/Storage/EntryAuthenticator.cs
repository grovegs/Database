using System.Security.Cryptography;
using System.Text;

namespace GroveGames.Database.Storage;

internal sealed class EntryAuthenticator : IDisposable
{
    public const int TagLength = 16;

    private const int HashLength = 32;

#if NET6_0_OR_GREATER
    private readonly byte[] _fileKey;
#else
    private readonly HMACSHA256 _hmac;
#endif

    public EntryAuthenticator(byte[] key, string name)
    {
        using var derivation = new HMACSHA256(key);
        var fileKey = derivation.ComputeHash(Encoding.UTF8.GetBytes(name));
#if NET6_0_OR_GREATER
        _fileKey = fileKey;
#else
        _hmac = new HMACSHA256(fileKey);
#endif
    }

    public void Compute(ReadOnlySpan<byte> data, Span<byte> tag)
    {
        Span<byte> hash = stackalloc byte[HashLength];
#if NET6_0_OR_GREATER
        HMACSHA256.HashData(_fileKey, data, hash);
#else
        _hmac.TryComputeHash(data, hash, out _);
#endif
        hash.Slice(0, TagLength).CopyTo(tag);
    }

    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> tag)
    {
        Span<byte> expected = stackalloc byte[TagLength];
        Compute(data, expected);
        return CryptographicOperations.FixedTimeEquals(expected, tag);
    }

    public void Dispose()
    {
#if !NET6_0_OR_GREATER
        _hmac.Dispose();
#endif
    }
}
