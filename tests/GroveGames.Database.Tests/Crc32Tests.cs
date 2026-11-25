using System.Text;
using GroveGames.Database.Storage;

namespace GroveGames.Database.Tests;

public sealed class Crc32Tests
{
    [Fact]
    public void Compute_CheckString_ReturnsStandardValue()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void Compute_EveryLength_MatchesBytewiseValue()
    {
        var data = new byte[70];
        new Random(7).NextBytes(data);

        for (var length = 0; length <= data.Length; length++)
        {
            Assert.Equal(Bytewise(data.AsSpan(0, length)), Crc32.Compute(data.AsSpan(0, length)));
        }
    }

    private static uint Bytewise(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in data)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }
}
