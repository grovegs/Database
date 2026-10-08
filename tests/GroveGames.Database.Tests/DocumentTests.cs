using GroveGames.Serialization;

namespace GroveGames.Database.Tests;

public sealed class DocumentTests : IDisposable
{
    private readonly string _directory;

    public DocumentTests()
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
    public void Value_NeverSet_ReturnsNewInstance()
    {
        using var database = Open();

        var settings = database.GetDocument<DocumentSettings>("settings");

        Assert.NotNull(settings.Value);
        Assert.Equal(1f, settings.Value.Volume);
    }

    [Fact]
    public void Set_ThenReopen_ReturnsLatestValue()
    {
        using (var database = Open())
        {
            var settings = database.GetDocument<DocumentSettings>("settings");
            settings.Set(new DocumentSettings { Volume = 0.2f, Language = "tr" });
            settings.Set(new DocumentSettings { Volume = 0.5f, Language = "en" });
        }

        using var reopened = Open();
        var loaded = reopened.GetDocument<DocumentSettings>("settings").Value;

        Assert.Equal(0.5f, loaded.Volume);
        Assert.Equal("en", loaded.Language);
    }

    [Fact]
    public void Reopen_ManySets_CompactsToOneEntry()
    {
        using (var database = Open())
        {
            var settings = database.GetDocument<DocumentSettings>("settings");

            for (var i = 0; i < 50; i++)
            {
                settings.Set(new DocumentSettings { Volume = i });
            }
        }

        var path = Path.Combine(_directory, "settings.db");
        var before = new FileInfo(path).Length;

        using (var database = Open())
        {
            Assert.Equal(49f, database.GetDocument<DocumentSettings>("settings").Value.Volume);
        }

        Assert.True(new FileInfo(path).Length < before / 10);
    }

    private FileDatabase Open()
    {
        return new FileDatabase(_directory, new FormatterRegistryBuilder().AddGroveGamesDatabaseTestsFormatters().Build());
    }
}

[Schema]
public sealed class DocumentSettings
{
    public float Volume = 1f;
    public string? Language;
}
