using System.Buffers.Binary;
using GroveGames.Database.Storage;
using GroveGames.Serialization;

namespace GroveGames.Database.Tests;

public sealed class ProtectionTests : IDisposable
{
    private static readonly byte[] s_key = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    private readonly string _directory;

    public ProtectionTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "GroveGames.Database.Tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Fact]
    public void Reopen_WithKey_ReturnsValues()
    {
        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 120 });
            database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id).Upsert(new ProtectedItem { Id = 1, Count = 3 });
        }

        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));

        Assert.Equal(120, reopened.GetDocument<ProtectedWallet>("wallet").Value.Gold);
        Assert.Equal(3, reopened.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id).Get(1).Count);
        Assert.Equal(2, File.ReadAllBytes(PathOf("wallet"))[4]);
    }

    [Fact]
    public void Reopen_EditedEntryWithValidChecksum_ThrowsDatabaseTamperedException()
    {
        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 120 });
        }

        var contents = File.ReadAllBytes(PathOf("wallet"));
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(contents.AsSpan(8));
        contents[13 + payloadLength - 1] ^= 0x01;
        BinaryPrimitives.WriteUInt32LittleEndian(contents.AsSpan(13 + payloadLength), Crc32.Compute(contents.AsSpan(12, payloadLength + 1)));
        File.WriteAllBytes(PathOf("wallet"), contents);

        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));

        Assert.Throws<DatabaseTamperedException>(() => reopened.GetDocument<ProtectedWallet>("wallet"));
    }

    [Fact]
    public void Reopen_WithAnotherKey_ThrowsDatabaseTamperedException()
    {
        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 120 });
        }

        using var reopened = Open(DatabaseProtection.TamperCheck([.. Enumerable.Reverse(s_key)]));

        Assert.Throws<DatabaseTamperedException>(() => reopened.GetDocument<ProtectedWallet>("wallet"));
    }

    [Fact]
    public void Reopen_FileCopiedFromAnotherStore_ThrowsDatabaseTamperedException()
    {
        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            database.GetDocument<ProtectedWallet>("rich").Set(new ProtectedWallet { Gold = 999_999 });
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 1 });
        }

        File.Copy(PathOf("rich"), PathOf("wallet"), true);
        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));

        Assert.Throws<DatabaseTamperedException>(() => reopened.GetDocument<ProtectedWallet>("wallet"));
    }

    [Fact]
    public void Reopen_PlainFileWithKey_ProtectsIt()
    {
        using (var database = Open(DatabaseProtection.None))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 75 });
        }

        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            Assert.Equal(75, database.GetDocument<ProtectedWallet>("wallet").Value.Gold);
        }

        Assert.Equal(2, File.ReadAllBytes(PathOf("wallet"))[4]);

        using var withoutKey = Open(DatabaseProtection.None);

        Assert.Throws<InvalidOperationException>(() => withoutKey.GetDocument<ProtectedWallet>("wallet"));
    }

    [Fact]
    public void Reopen_TornTailWithKey_KeepsEarlierEntries()
    {
        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            var items = database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id);
            items.Upsert(new ProtectedItem { Id = 1, Count = 1 });
            items.Upsert(new ProtectedItem { Id = 2, Count = 2 });
        }

        using (var stream = new FileStream(PathOf("items"), FileMode.Open))
        {
            stream.SetLength(stream.Length - 3);
        }

        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));
        var reloaded = reopened.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id);

        Assert.True(reloaded.Contains(1));
        Assert.False(reloaded.Contains(2));
    }

    [Fact]
    public void Reopen_CompactedWithKey_ReturnsLatestValues()
    {
        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            var items = database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id);

            for (var i = 0; i < 40; i++)
            {
                items.Upsert(new ProtectedItem { Id = 1, Count = i });
            }
        }

        var before = new FileInfo(PathOf("items")).Length;

        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            Assert.Equal(39, database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id).Get(1).Count);
        }

        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));

        Assert.True(new FileInfo(PathOf("items")).Length < before);
        Assert.Equal(39, reopened.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id).Get(1).Count);
    }

    [Fact]
    public void TamperCheck_ShortKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DatabaseProtection.TamperCheck(new byte[15]));
    }

    private FileDatabase Open(DatabaseProtection protection)
    {
        return new FileDatabase(_directory, protection);
    }

    private string PathOf(string name)
    {
        return Path.Combine(_directory, name + ".db");
    }
}

[Schema]
public sealed class ProtectedWallet
{
    public long Gold;
}

[Schema]
public sealed class ProtectedItem
{
    public int Id;
    public int Count;
}
