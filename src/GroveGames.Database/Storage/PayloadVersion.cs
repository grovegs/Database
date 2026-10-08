using System.Buffers.Binary;

namespace GroveGames.Database.Storage;

internal static class PayloadVersion
{
    private const byte VersionExtension = 0x56;
    private const byte FixedExtension1 = 0xd4;
    private const byte FixedExtension4 = 0xd6;

    public static int Read(ReadOnlySpan<byte> payload)
    {
        if (payload.Length >= 3 && payload[0] == FixedExtension1 && payload[1] == VersionExtension)
        {
            return payload[2];
        }

        if (payload.Length >= 6 && payload[0] == FixedExtension4 && payload[1] == VersionExtension)
        {
            return (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(2)), int.MaxValue);
        }

        return 1;
    }
}
