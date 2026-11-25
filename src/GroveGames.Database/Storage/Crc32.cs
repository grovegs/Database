using System.Buffers.Binary;

namespace GroveGames.Database.Storage;

internal static class Crc32
{
    private static readonly uint[] s_table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var table = s_table;
        var crc = 0xFFFFFFFFu;

        while (data.Length >= 8)
        {
            var low = BinaryPrimitives.ReadUInt32LittleEndian(data) ^ crc;
            var high = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4));
            crc = table[0x700 + (low & 0xFF)]
                ^ table[0x600 + ((low >> 8) & 0xFF)]
                ^ table[0x500 + ((low >> 16) & 0xFF)]
                ^ table[0x400 + (low >> 24)]
                ^ table[0x300 + (high & 0xFF)]
                ^ table[0x200 + ((high >> 8) & 0xFF)]
                ^ table[0x100 + ((high >> 16) & 0xFF)]
                ^ table[high >> 24];
            data = data.Slice(8);
        }

        for (var i = 0; i < data.Length; i++)
        {
            crc = table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[8 * 256];

        for (var i = 0u; i < 256; i++)
        {
            var value = i;

            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        for (var i = 256; i < table.Length; i++)
        {
            var previous = table[i - 256];
            table[i] = (previous >> 8) ^ table[previous & 0xFF];
        }

        return table;
    }
}
