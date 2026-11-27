using System.Security.Cryptography;
using System.Text;

namespace GroveGames.Database.Storage;

internal sealed class HmacEntrySigner : IEntrySigner
{
    private const int Length = 16;
    private const string ChainedSuffix = "/chained";

    private readonly HmacSha256 _hash;
    private readonly HmacSha256 _unchainedHash;
    private readonly bool _trustsExistingTags;

    public HmacEntrySigner(byte[] key, string name, bool trustsExistingTags)
    {
        _trustsExistingTags = trustsExistingTags;
        using var derivation = new HMACSHA256(key);
        _hash = new HmacSha256(derivation.ComputeHash(Encoding.UTF8.GetBytes(name + ChainedSuffix)));
        _unchainedHash = new HmacSha256(derivation.ComputeHash(Encoding.UTF8.GetBytes(name)));
    }

    public int TagLength => Length;

    public bool TrustsExistingTags => _trustsExistingTags;

    public void Sign(ReadOnlySpan<byte> previousTag, ReadOnlySpan<byte> data, Span<byte> tag)
    {
        Compute(_hash, previousTag, data, tag);
    }

    public bool Verify(ReadOnlySpan<byte> previousTag, ReadOnlySpan<byte> data, ReadOnlySpan<byte> tag)
    {
        Span<byte> expected = stackalloc byte[Length];
        Sign(previousTag, data, expected);
        return CryptographicOperations.FixedTimeEquals(expected, tag);
    }

    public bool VerifyUnchained(ReadOnlySpan<byte> data, ReadOnlySpan<byte> tag)
    {
        Span<byte> expected = stackalloc byte[Length];
        Compute(_unchainedHash, [], data, expected);
        return CryptographicOperations.FixedTimeEquals(expected, tag);
    }

    public void Dispose()
    {
    }

    // Unity's IncrementalHash copies spans into new arrays, so the hash is computed in managed code to avoid an allocation per entry.
    private static void Compute(HmacSha256 hash, ReadOnlySpan<byte> previousTag, ReadOnlySpan<byte> data, Span<byte> tag)
    {
        Span<byte> result = stackalloc byte[HmacSha256.HashLength];
        hash.Compute(previousTag, data, result);
        result.Slice(0, Length).CopyTo(tag);
    }
}
