using System.Security.Cryptography;
using System.Text;
using GroveGames.Database.Storage;

namespace GroveGames.Database.Tests;

public sealed class HmacSha256Tests
{
    [Fact]
    public void Compute_Rfc4231Case2_ReturnsKnownValue()
    {
        var hash = new HmacSha256(Encoding.ASCII.GetBytes("Jefe"));
        var result = new byte[HmacSha256.HashLength];

        hash.Compute(Encoding.ASCII.GetBytes("what do ya want "), Encoding.ASCII.GetBytes("for nothing?"), result);

        Assert.Equal("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843", Convert.ToHexString(result).ToLowerInvariant());
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(100)]
    public void Compute_EverySplitAndLength_MatchesSystemHmac(int keyLength)
    {
        var random = new Random(keyLength);
        var key = new byte[keyLength];
        random.NextBytes(key);
        var data = new byte[200];
        random.NextBytes(data);
        var hash = new HmacSha256(key);
        using var system = new HMACSHA256(key);
        var result = new byte[HmacSha256.HashLength];

        for (var length = 0; length <= data.Length; length++)
        {
            var split = length / 3;
            hash.Compute(data.AsSpan(0, split), data.AsSpan(split, length - split), result);

            Assert.Equal(system.ComputeHash(data, 0, length), result);
        }
    }

    [Fact]
    public void Compute_Repeated_DoesNotAllocate()
    {
        var hash = new HmacSha256(new byte[32]);
        var data = new byte[300];
        var result = new byte[HmacSha256.HashLength];
        hash.Compute(data.AsSpan(0, 16), data, result);

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 100; i++)
        {
            hash.Compute(data.AsSpan(0, 16), data, result);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
