namespace GroveGames.Database.Storage;

internal static class Crc32
{
    private static readonly uint[] s_table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        for (var i = 0; i < data.Length; i++)
        {
            crc = s_table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];

        for (var i = 0u; i < 256; i++)
        {
            var value = i;

            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
