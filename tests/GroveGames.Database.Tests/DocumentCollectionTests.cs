using GroveGames.Serialization;

namespace GroveGames.Database.Tests;

public sealed class DocumentCollectionTests : IDisposable
{
    private readonly string _directory;

    public DocumentCollectionTests()
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
    public void Upsert_ThenReopen_LoadsLatestValues()
    {
        using (var database = Open())
        {
            var players = Players(database);
            players.Upsert(new CollectionPlayer { Id = 1, Name = "a", Gold = 10 });
            players.Upsert(new CollectionPlayer { Id = 2, Name = "b", Gold = 20 });
            players.Upsert(new CollectionPlayer { Id = 1, Name = "a", Gold = 15 });
        }

        using var reopened = Open();
        var loaded = Players(reopened);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(15, loaded.Get(1).Gold);
        Assert.Equal("b", loaded.Get(2).Name);
    }

    [Fact]
    public void Remove_ThenReopen_KeyIsGone()
    {
        using (var database = Open())
        {
            var players = Players(database);
            players.Upsert(new CollectionPlayer { Id = 1 });
            players.Upsert(new CollectionPlayer { Id = 2 });
            Assert.True(players.Remove(1));
            Assert.False(players.Remove(3));
        }

        using var reopened = Open();
        var loaded = Players(reopened);

        Assert.False(loaded.Contains(1));
        Assert.True(loaded.Contains(2));
    }

    [Fact]
    public void Clear_ThenReopen_IsEmpty()
    {
        using (var database = Open())
        {
            var players = Players(database);
            players.Upsert(new CollectionPlayer { Id = 1 });
            players.Clear();
            players.Upsert(new CollectionPlayer { Id = 2 });
        }

        using var reopened = Open();
        var loaded = Players(reopened);

        Assert.Single(loaded);
        Assert.True(loaded.Contains(2));
    }

    [Fact]
    public void Reopen_TornLastEntry_KeepsEarlierEntriesAndAcceptsNewWrites()
    {
        using (var database = Open())
        {
            var players = Players(database);
            players.Upsert(new CollectionPlayer { Id = 1, Name = "kept" });
            players.Upsert(new CollectionPlayer { Id = 2, Name = "torn" });
        }

        var path = Path.Combine(_directory, "players.db");
        var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes.AsSpan(0, bytes.Length - 3).ToArray());

        using (var database = Open())
        {
            var players = Players(database);
            Assert.Equal("kept", players.Get(1).Name);
            Assert.False(players.Contains(2));
            players.Upsert(new CollectionPlayer { Id = 3, Name = "after" });
        }

        using var reopened = Open();
        Assert.Equal("after", Players(reopened).Get(3).Name);
    }

    [Fact]
    public void Reopen_CorruptedLastEntry_DiscardsIt()
    {
        using (var database = Open())
        {
            var players = Players(database);
            players.Upsert(new CollectionPlayer { Id = 1, Name = "kept" });
            players.Upsert(new CollectionPlayer { Id = 2, Name = "corrupt" });
        }

        var path = Path.Combine(_directory, "players.db");
        var bytes = File.ReadAllBytes(path);
        bytes[^6] ^= 0xFF;
        File.WriteAllBytes(path, bytes);

        using var reopened = Open();
        var loaded = Players(reopened);

        Assert.True(loaded.Contains(1));
        Assert.False(loaded.Contains(2));
    }

    [Fact]
    public void Reopen_ManyOverwrites_CompactsFile()
    {
        using (var database = Open())
        {
            var players = Players(database);

            for (var i = 0; i < 100; i++)
            {
                players.Upsert(new CollectionPlayer { Id = 1, Gold = i });
            }
        }

        var path = Path.Combine(_directory, "players.db");
        var before = new FileInfo(path).Length;

        using (var database = Open())
        {
            Assert.Equal(99, Players(database).Get(1).Gold);
        }

        Assert.True(new FileInfo(path).Length < before / 10);

        using var reopened = Open();
        Assert.Equal(99, Players(reopened).Get(1).Gold);
    }

    [Fact]
    public void Reopen_LeftoverCompactionFile_IsRemoved()
    {
        using (var database = Open())
        {
            Players(database).Upsert(new CollectionPlayer { Id = 1, Name = "kept" });
        }

        var temporary = Path.Combine(_directory, "players.db.tmp");
        File.WriteAllBytes(temporary, [1, 2, 3]);

        using var reopened = Open();

        Assert.Equal("kept", Players(reopened).Get(1).Name);
        Assert.False(File.Exists(temporary));
    }

    [Fact]
    public void Reopen_OlderVersionEntries_AreMigrated()
    {
        using (var database = Open())
        {
            database.GetDocumentCollection<int, CollectionPlayer>("profiles", player => player.Id).Upsert(new CollectionPlayer { Id = 1, Name = "old", Gold = 50 });
        }

        using var reopened = Open();
        var profiles = reopened.GetDocumentCollection<int, CollectionProfile>("profiles", profile => profile.Id);

        Assert.Equal("old", profiles.Get(1).DisplayName);
    }

    [Fact]
    public void Upsert_StringAndGuidKeys_RoundTrip()
    {
        var id = Guid.NewGuid();

        using (var database = Open())
        {
            database.GetDocumentCollection<string, CollectionPlayer>("named", player => player.Name!).Upsert(new CollectionPlayer { Name = "hero" });
            database.GetDocumentCollection<Guid, CollectionTagged>("tagged", tagged => Guid.Parse(tagged.Tag!)).Upsert(new CollectionTagged { Tag = id.ToString() });
        }

        using var reopened = Open();

        Assert.True(reopened.GetDocumentCollection<string, CollectionPlayer>("named", player => player.Name!).Contains("hero"));
        Assert.True(reopened.GetDocumentCollection<Guid, CollectionTagged>("tagged", tagged => Guid.Parse(tagged.Tag!)).Contains(id));
    }

    [Fact]
    public void GetEnumerator_ReturnsEveryValue()
    {
        using var database = Open();
        var players = Players(database);
        players.Upsert(new CollectionPlayer { Id = 1, Gold = 1 });
        players.Upsert(new CollectionPlayer { Id = 2, Gold = 2 });

        var total = 0L;

        foreach (var player in players)
        {
            total += player.Gold;
        }

        Assert.Equal(3, total);
    }

    private FileDatabase Open()
    {
        return new FileDatabase(_directory, new FormatterRegistryBuilder().AddGroveGamesDatabaseTestsFormatters().Build());
    }

    private static IDocumentCollection<int, CollectionPlayer> Players(FileDatabase database)
    {
        return database.GetDocumentCollection<int, CollectionPlayer>("players", player => player.Id);
    }
}

[Schema]
public sealed class CollectionPlayer
{
    public int Id;
    public string? Name;
    public long Gold;
}

[Schema(version: 2)]
public sealed class CollectionProfile
{
    public int Id;
    public string? DisplayName;
}

public sealed class CollectionProfileRenameName : IMigration<CollectionProfile>
{
    public int FromVersion => 1;

    public void Apply(DataValue root)
    {
        root.AsObject.Rename("name", "displayName");
    }
}

[Schema]
public sealed class CollectionTagged
{
    public string? Tag;
}
