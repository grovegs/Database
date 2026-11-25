using System.Security.Cryptography;
using System.Text;

namespace GroveGames.Database.Storage;

internal sealed class HmacEntrySigner : IEntrySigner
{
    private const int Length = 16;
    private const int HashLength = 32;

    private const string ChainedSuffix = "/chained";

    private readonly IncrementalHash _hash;
    private readonly IncrementalHash _unchainedHash;
    private readonly bool _trustsExistingTags;

    public HmacEntrySigner(byte[] key, string name, bool trustsExistingTags)
    {
        _trustsExistingTags = trustsExistingTags;
        using var derivation = new HMACSHA256(key);
        _hash = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, derivation.ComputeHash(Encoding.UTF8.GetBytes(name + ChainedSuffix)));
        _unchainedHash = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, derivation.ComputeHash(Encoding.UTF8.GetBytes(name)));
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
        _hash.Dispose();
        _unchainedHash.Dispose();
    }

    private static void Compute(IncrementalHash hash, ReadOnlySpan<byte> previousTag, ReadOnlySpan<byte> data, Span<byte> tag)
    {
        Span<byte> result = stackalloc byte[HashLength];
        hash.AppendData(previousTag);
        hash.AppendData(data);
        hash.TryGetHashAndReset(result, out _);
        result.Slice(0, Length).CopyTo(tag);
    }
}
