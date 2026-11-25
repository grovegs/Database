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

    [Fact]
    public void Save_ManySets_CompactsWithoutReopening()
    {
        var path = Path.Combine(_directory, "settings.db");
        using var database = Open();
        var settings = database.GetDocument<DocumentSettings>("settings");

        for (var i = 0; i < 50; i++)
        {
            settings.Set(new DocumentSettings { Volume = i });
        }

        var before = new FileInfo(path).Length;
        database.Save();
        settings.Set(new DocumentSettings { Volume = 50 });

        Assert.True(new FileInfo(path).Length < before / 10);
        Assert.Equal(50f, settings.Value.Volume);
    }

    [Fact]
    public void Reopen_OlderVersionValue_IsMigratedOnce()
    {
        using (var database = Open())
        {
            database.GetDocument<DocumentSettings>("audio").Set(new DocumentSettings { Volume = 0.25f, Language = "tr" });
        }

        DocumentAudioRenameVolume.Applied = 0;

        using (var database = Open())
        {
            Assert.Equal(0.25f, database.GetDocument<DocumentAudio>("audio").Value.Master);
        }

        using var reopened = Open();
        var audio = reopened.GetDocument<DocumentAudio>("audio").Value;

        Assert.Equal(1, DocumentAudioRenameVolume.Applied);
        Assert.Equal(0.25f, audio.Master);
        Assert.Equal("tr", audio.Language);
    }

    [Fact]
    public void Save_CompactionFails_KeepsLaterWrites()
    {
        using (var database = Open())
        {
            var document = database.GetDocument<FragileValue>("fragile");

            for (var i = 0; i < 17; i++)
            {
                document.Set(new FragileValue { Gold = i });
            }

            FragileValueFormatter.FailWrites = true;

            try
            {
                Assert.Throws<InvalidOperationException>(database.Save);
            }
            finally
            {
                FragileValueFormatter.FailWrites = false;
            }

            document.Set(new FragileValue { Gold = 99 });
            database.Save();
        }

        using var reopened = Open();

        Assert.Equal(99, reopened.GetDocument<FragileValue>("fragile").Value.Gold);
    }

    private FileDatabase Open()
    {
        return new FileDatabase(_directory);
    }
}

[Schema]
public sealed class DocumentSettings
{
    public float Volume = 1f;
    public string? Language;
}

[Schema(version: 2)]
public sealed class DocumentAudio
{
    public float Master;
    public string? Language;
}

public sealed class DocumentAudioRenameVolume : IMigration<DocumentAudio>
{
    public static int Applied;

    public int FromVersion => 1;

    public void Apply(DataValue root)
    {
        Applied++;
        root.AsObject.Rename("volume", "master");
    }
}

public sealed class FragileValue
{
    public long Gold;
}

[Formatter]
public sealed class FragileValueFormatter : IFormatter<FragileValue>
{
    public static bool FailWrites;

    public void Write<TWriter>(ref TWriter writer, FragileValue? value) where TWriter : struct, IFormatWriter
    {
        if (FailWrites)
        {
            throw new InvalidOperationException("The write failed.");
        }

        writer.WriteInt64(value?.Gold ?? 0);
    }

    public FragileValue? Read<TReader>(ref TReader reader) where TReader : struct, IFormatReader
    {
        return new FragileValue { Gold = reader.ReadInt64() };
    }

    public void Transcode<TReader, TWriter>(ref TReader reader, ref TWriter writer)
        where TReader : struct, IFormatReader
        where TWriter : struct, IFormatWriter
    {
        writer.WriteInt64(reader.ReadInt64());
    }
}
