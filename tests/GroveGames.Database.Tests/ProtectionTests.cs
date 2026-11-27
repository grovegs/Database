using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
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
        Assert.Equal(3, File.ReadAllBytes(PathOf("wallet"))[4]);
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
    public void Reopen_NewKeyTrustingExistingFiles_ResignsThem()
    {
        byte[] newKey = [.. Enumerable.Reverse(s_key)];

        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 120 });
            database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id).Upsert(new ProtectedItem { Id = 1, Count = 3 });
        }

        using (var database = Open(DatabaseProtection.TamperCheck(newKey, true)))
        {
            Assert.Equal(120, database.GetDocument<ProtectedWallet>("wallet").Value.Gold);
            Assert.Equal(3, database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id).Get(1).Count);
        }

        using (var database = Open(DatabaseProtection.TamperCheck(newKey)))
        {
            Assert.Equal(120, database.GetDocument<ProtectedWallet>("wallet").Value.Gold);
            Assert.Equal(3, database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id).Get(1).Count);
        }

        using var withOldKey = Open(DatabaseProtection.TamperCheck(s_key));

        Assert.Throws<DatabaseTamperedException>(() => withOldKey.GetDocument<ProtectedWallet>("wallet"));
    }

    [Fact]
    public void Reopen_NewKeyTrustingExistingFiles_SignsLaterWrites()
    {
        byte[] newKey = [.. Enumerable.Reverse(s_key)];

        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 1 });
        }

        using (var database = Open(DatabaseProtection.TamperCheck(newKey, true)))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 2 });
        }

        using var reopened = Open(DatabaseProtection.TamperCheck(newKey));

        Assert.Equal(2, reopened.GetDocument<ProtectedWallet>("wallet").Value.Gold);
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
    public void Reopen_PlainFileWithKey_ThrowsDatabaseTamperedException()
    {
        using (var database = Open(DatabaseProtection.None))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 999_999 });
        }

        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));

        Assert.Throws<DatabaseTamperedException>(() => reopened.GetDocument<ProtectedWallet>("wallet"));
    }

    [Fact]
    public void Reopen_EntryRemovedFromMiddle_ThrowsDatabaseTamperedException()
    {
        WriteThreeItems();
        var contents = File.ReadAllBytes(PathOf("items"));
        var first = EntryLength(contents, 8);
        var second = EntryLength(contents, 8 + first);
        File.WriteAllBytes(PathOf("items"), [.. contents.AsSpan(0, 8 + first), .. contents.AsSpan(8 + first + second)]);

        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));

        Assert.Throws<DatabaseTamperedException>(() => reopened.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id));
    }

    [Fact]
    public void Reopen_EntriesReordered_ThrowsDatabaseTamperedException()
    {
        WriteThreeItems();
        var contents = File.ReadAllBytes(PathOf("items"));
        var first = EntryLength(contents, 8);
        var second = EntryLength(contents, 8 + first);
        File.WriteAllBytes(PathOf("items"), [.. contents.AsSpan(0, 8), .. contents.AsSpan(8 + first, second), .. contents.AsSpan(8, first), .. contents.AsSpan(8 + first + second)]);

        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));

        Assert.Throws<DatabaseTamperedException>(() => reopened.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id));
    }

    [Fact]
    public void Reopen_ChainedEntriesReframedAsVersionTwo_ThrowsDatabaseTamperedException()
    {
        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            var wallet = database.GetDocument<ProtectedWallet>("wallet");
            wallet.Set(new ProtectedWallet { Gold = 1 });
            wallet.Set(new ProtectedWallet { Gold = 2 });
        }

        var contents = File.ReadAllBytes(PathOf("wallet"));
        var reframed = new List<byte>(contents.AsSpan(0, 8).ToArray());
        reframed[4] = 2;
        var previousTag = new byte[16];
        var position = 8;

        while (position < contents.Length)
        {
            var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(contents.AsSpan(position));
            var tag = contents.AsSpan(position + 9 + payloadLength, 16).ToArray();
            byte[] signed = [.. previousTag, .. contents.AsSpan(position + 4, payloadLength + 1)];
            var entry = new byte[4 + signed.Length + 4];
            BinaryPrimitives.WriteInt32LittleEndian(entry, signed.Length - 1);
            signed.CopyTo(entry.AsSpan(4));
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(4 + signed.Length), Crc32.Compute(signed));
            reframed.AddRange(entry);
            reframed.AddRange(tag);
            previousTag = tag;
            position += 9 + payloadLength + 16;
        }

        File.WriteAllBytes(PathOf("wallet"), [.. reframed]);
        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));

        Assert.Throws<DatabaseTamperedException>(() => reopened.GetDocument<ProtectedWallet>("wallet"));
    }

    [Fact]
    public void Reopen_UnchainedVersionTwoFile_ReadsAndUpgradesIt()
    {
        Directory.CreateDirectory(_directory);
        var payload = new ByteBuffer();
        new MessagePackSerializer().Serialize(new ProtectedWallet { Gold = 42 }, payload);
        var signed = new byte[payload.Length + 1];
        signed[0] = 1;
        payload.WrittenSpan.CopyTo(signed.AsSpan(1));
        using var derivation = new HMACSHA256(s_key);
        using var hmac = new HMACSHA256(derivation.ComputeHash(Encoding.UTF8.GetBytes("wallet")));
        var entry = new byte[4 + signed.Length + 4 + 16];
        BinaryPrimitives.WriteInt32LittleEndian(entry, payload.Length);
        signed.CopyTo(entry.AsSpan(4));
        BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(4 + signed.Length), Crc32.Compute(signed));
        hmac.ComputeHash(signed).AsSpan(0, 16).CopyTo(entry.AsSpan(8 + signed.Length));
        File.WriteAllBytes(PathOf("wallet"), [(byte)'G', (byte)'G', (byte)'D', (byte)'B', 2, 1, 0, 0, .. entry]);

        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            Assert.Equal(42, database.GetDocument<ProtectedWallet>("wallet").Value.Gold);
        }

        Assert.Equal(3, File.ReadAllBytes(PathOf("wallet"))[4]);
        using var reopened = Open(DatabaseProtection.TamperCheck(s_key));
        Assert.Equal(42, reopened.GetDocument<ProtectedWallet>("wallet").Value.Gold);
    }

    [Fact]
    public void Reopen_PlainFileWithKeyTrustingExistingFiles_ProtectsIt()
    {
        using (var database = Open(DatabaseProtection.None))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 75 });
        }

        using (var database = Open(DatabaseProtection.TamperCheck(s_key, true)))
        {
            Assert.Equal(75, database.GetDocument<ProtectedWallet>("wallet").Value.Gold);
        }

        Assert.Equal(3, File.ReadAllBytes(PathOf("wallet"))[4]);

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

    private void WriteThreeItems()
    {
        using var database = Open(DatabaseProtection.TamperCheck(s_key));
        var items = database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id);
        items.Upsert(new ProtectedItem { Id = 1, Count = 1 });
        items.Upsert(new ProtectedItem { Id = 1, Count = 0 });
        items.Upsert(new ProtectedItem { Id = 2, Count = 2 });
    }

    private static int EntryLength(byte[] contents, int start)
    {
        return 4 + 1 + BinaryPrimitives.ReadInt32LittleEndian(contents.AsSpan(start)) + 4 + 16;
    }

    [Fact]
    public void Open_TrustingANewKey_ResignsStoresThatAreNotOpened()
    {
        byte[] newKey = [.. Enumerable.Range(40, 32).Select(value => (byte)value)];

        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 7 });
            database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id).Upsert(new ProtectedItem { Id = 1, Count = 3 });
        }

        using (var trusting = Open(DatabaseProtection.TamperCheck(newKey, true)))
        {
            trusting.GetDocument<ProtectedWallet>("wallet");
        }

        using var reopened = Open(DatabaseProtection.TamperCheck(newKey));

        Assert.Equal(3, reopened.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id).Get(1).Count);
        Assert.Equal(7, reopened.GetDocument<ProtectedWallet>("wallet").Value.Gold);
    }

    [Fact]
    public void Open_TrustingAndEverythingSigned_ReportsItAndStopsTrusting()
    {
        using (var database = Open(DatabaseProtection.TamperCheck(s_key)))
        {
            database.GetDocument<ProtectedWallet>("wallet").Set(new ProtectedWallet { Gold = 7 });
        }

        byte[] newKey = [.. Enumerable.Range(40, 32).Select(value => (byte)value)];
        using var trusting = Open(DatabaseProtection.TamperCheck(newKey, true));

        Assert.True(trusting.AreExistingStoresSigned);
        Assert.Equal(7, trusting.GetDocument<ProtectedWallet>("wallet").Value.Gold);
    }

    [Fact]
    public void Set_Protected_DoesNotAllocatePerWrite()
    {
        using var database = Open(DatabaseProtection.TamperCheck(s_key));
        var items = database.GetDocumentCollection<int, ProtectedItem>("items", item => item.Id);
        var item = new ProtectedItem { Id = 1, Count = 0 };
        items.Upsert(item);

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 50; i++)
        {
            item.Count = i;
            items.Upsert(item);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
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
