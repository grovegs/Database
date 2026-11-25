using System.Security.Cryptography;
using System.Text;

namespace GroveGames.Database.Storage;

internal sealed class HmacEntrySigner : IEntrySigner
{
    private const int Length = 16;
    private const int HashLength = 32;

    private readonly IncrementalHash _hash;
    private readonly bool _trustsExistingTags;

    public HmacEntrySigner(byte[] key, string name, bool trustsExistingTags)
    {
        _trustsExistingTags = trustsExistingTags;
        using var derivation = new HMACSHA256(key);
        _hash = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, derivation.ComputeHash(Encoding.UTF8.GetBytes(name)));
    }

    public int TagLength => Length;

    public bool TrustsExistingTags => _trustsExistingTags;

    public void Sign(ReadOnlySpan<byte> previousTag, ReadOnlySpan<byte> data, Span<byte> tag)
    {
        Span<byte> hash = stackalloc byte[HashLength];
        _hash.AppendData(previousTag);
        _hash.AppendData(data);
        _hash.TryGetHashAndReset(hash, out _);
        hash.Slice(0, Length).CopyTo(tag);
    }

    public bool Verify(ReadOnlySpan<byte> previousTag, ReadOnlySpan<byte> data, ReadOnlySpan<byte> tag)
    {
        Span<byte> expected = stackalloc byte[Length];
        Sign(previousTag, data, expected);
        return CryptographicOperations.FixedTimeEquals(expected, tag);
    }

    public void Dispose()
    {
        _hash.Dispose();
    }
}
