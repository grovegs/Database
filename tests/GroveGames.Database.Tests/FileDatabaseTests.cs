using System.Text;
using GroveGames.Serialization;

namespace GroveGames.Database.Tests;

public sealed class FileDatabaseTests : IDisposable
{
    private readonly string _directory;

    public FileDatabaseTests()
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
    public void GetDocumentCollection_SameName_ReturnsSameInstance()
    {
        using var database = Open();

        var first = database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id);
        var second = database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetDocument_NameOpenAsCollection_ThrowsInvalidOperationException()
    {
        using var database = Open();
        database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id);

        Assert.Throws<InvalidOperationException>(() => database.GetDocument<DatabaseItem>("items"));
    }

    [Fact]
    public void GetDocument_FileStoredAsCollection_ThrowsFormatException()
    {
        using (var database = Open())
        {
            database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id).Upsert(new DatabaseItem { Id = 1 });
        }

        using var reopened = Open();

        Assert.Throws<FormatException>(() => reopened.GetDocument<DatabaseItem>("items"));
    }

    [Fact]
    public void GetDocument_UnreadableFileWithHandler_QuarantinesItAndOpensEmpty()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "profile.db");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var reported = new List<string>();
        using var database = new FileDatabase(_directory, DatabaseProtection.None, (name, _) => reported.Add(name));

        var profile = database.GetDocument<DatabaseItem>("profile");
        profile.Set(new DatabaseItem { Id = 3 });

        Assert.Equal(["profile"], reported);
        Assert.True(File.Exists(path + ".corrupt"));
        Assert.Equal(3, profile.Value.Id);
    }

    [Fact]
    public void GetDocument_UnreadableFileWithoutHandler_Throws()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "profile.db"), [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        using var database = Open();

        Assert.Throws<FormatException>(() => database.GetDocument<DatabaseItem>("profile"));
    }

    [Fact]
    public void GetDocument_TamperedFileWithHandler_QuarantinesIt()
    {
        var key = new byte[32];

        using (var database = new FileDatabase(_directory, DatabaseProtection.TamperCheck(key)))
        {
            database.GetDocument<DatabaseItem>("profile").Set(new DatabaseItem { Id = 1 });
        }

        var path = Path.Combine(_directory, "profile.db");
        var bytes = File.ReadAllBytes(path);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
        var reported = 0;
        using var reopened = new FileDatabase(_directory, DatabaseProtection.TamperCheck(key), (_, _) => reported++);

        var profile = reopened.GetDocument<DatabaseItem>("profile");

        Assert.Equal(1, reported);
        Assert.Equal(0, profile.Value.Id);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("with space")]
    [InlineData("")]
    public void GetDocument_InvalidName_ThrowsArgumentException(string name)
    {
        using var database = Open();

        Assert.ThrowsAny<ArgumentException>(() => database.GetDocument<DatabaseItem>(name));
    }

    [Fact]
    public void GetDocumentCollection_UnsupportedKey_ThrowsNotSupportedException()
    {
        using var database = Open();

        Assert.Throws<NotSupportedException>(() => database.GetDocumentCollection<double, DatabaseItem>("items", item => item.Id));
    }

    [Fact]
    public void Save_AfterWrites_KeepsData()
    {
        using (var database = Open())
        {
            database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id).Upsert(new DatabaseItem { Id = 7 });
            database.Save();
        }

        using var reopened = Open();
        Assert.True(reopened.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id).Contains(7));
    }

    [Fact]
    public void GetDocument_AfterDispose_ThrowsObjectDisposedException()
    {
        var database = Open();
        database.Dispose();

        Assert.Throws<ObjectDisposedException>(() => database.GetDocument<DatabaseItem>("items"));
    }

    [Fact]
    public void OpenStoreNames_AfterOpeningStores_ListsEachName()
    {
        using var database = Open();
        database.GetDocument<DatabaseItem>("profile");
        database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id);

        var names = database.OpenStoreNames;

        Assert.Equal(2, names.Count);
        Assert.Contains("profile", names);
        Assert.Contains("items", names);
    }

    [Fact]
    public void TryInspect_Document_ReturnsCurrentValue()
    {
        using var database = Open();
        var item = new DatabaseItem { Id = 3 };
        database.GetDocument<DatabaseItem>("profile").Set(item);

        var found = database.TryInspect("profile", out var value);

        Assert.True(found);
        Assert.Same(item, value);
    }

    [Fact]
    public void TryInspect_Collection_ReturnsItems()
    {
        using var database = Open();
        var collection = database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id);
        collection.Upsert(new DatabaseItem { Id = 1 });
        collection.Upsert(new DatabaseItem { Id = 2 });

        var found = database.TryInspect("items", out var value);

        Assert.True(found);
        var items = Assert.IsAssignableFrom<IEnumerable<DatabaseItem>>(value);
        Assert.Equal(2, items.Count());
    }

    [Fact]
    public void TryWriteJson_Document_WritesStoredFields()
    {
        using var database = Open();
        database.GetDocument<DatabaseItem>("profile").Set(new DatabaseItem { Id = 3 });
        var output = new ByteBuffer();

        var found = database.TryWriteJson("profile", output);

        Assert.True(found);
        Assert.Equal("{\"id\":3}", Encoding.UTF8.GetString(output.WrittenSpan));
    }

    [Fact]
    public void TryWriteJson_Collection_WritesItemsAsArray()
    {
        using var database = Open();
        var collection = database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id);
        collection.Upsert(new DatabaseItem { Id = 1 });
        collection.Upsert(new DatabaseItem { Id = 2 });
        var output = new ByteBuffer();

        var found = database.TryWriteJson("items", output);

        Assert.True(found);
        Assert.Equal("[{\"id\":1},{\"id\":2}]", Encoding.UTF8.GetString(output.WrittenSpan));
    }

    [Fact]
    public void TryWriteJson_UnopenedName_ReturnsFalse()
    {
        using var database = Open();
        var output = new ByteBuffer();

        Assert.False(database.TryWriteJson("missing", output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void TryInspect_UnopenedName_ReturnsFalse()
    {
        using var database = Open();

        var found = database.TryInspect("missing", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void IsCollection_Collection_ReturnsTrue()
    {
        using var database = Open();
        database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id);

        var isCollection = database.IsCollection("items");

        Assert.True(isCollection);
    }

    [Fact]
    public void IsCollection_Document_ReturnsFalse()
    {
        using var database = Open();
        database.GetDocument<DatabaseItem>("profile");

        var isCollection = database.IsCollection("profile");

        Assert.False(isCollection);
    }

    [Fact]
    public void TryWrite_Document_PersistsValue()
    {
        using (var database = Open())
        {
            database.GetDocument<DatabaseItem>("profile");

            var written = database.TryWrite("profile", new DatabaseItem { Id = 7 });

            Assert.True(written);
        }

        using var reopened = Open();
        Assert.Equal(7, reopened.GetDocument<DatabaseItem>("profile").Value.Id);
    }

    [Fact]
    public void TryWrite_CollectionItem_UpsertsItem()
    {
        using var database = Open();
        var collection = database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id);

        database.TryWrite("items", new DatabaseItem { Id = 4 });

        Assert.True(collection.Contains(4));
    }

    [Fact]
    public void TryWrite_CollectionItemWithChangedKey_RemovesOldKey()
    {
        using var database = Open();
        var collection = database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id);
        var item = new DatabaseItem { Id = 1 };
        collection.Upsert(item);
        item.Id = 2;

        database.TryWrite("items", item);

        Assert.False(collection.Contains(1));
        Assert.Same(item, collection.Get(2));
        Assert.Single(collection);
    }

    [Fact]
    public void TryWrite_WrongType_Throws()
    {
        using var database = Open();
        database.GetDocument<DatabaseItem>("profile");

        Assert.Throws<ArgumentException>(() => database.TryWrite("profile", "text"));
    }

    [Fact]
    public void TryWrite_UnopenedName_ReturnsFalse()
    {
        using var database = Open();

        var written = database.TryWrite("missing", new DatabaseItem());

        Assert.False(written);
    }

    [Fact]
    public void Directory_ReturnsConstructorDirectory()
    {
        using var database = Open();

        Assert.Equal(_directory, database.Directory);
    }

    [Fact]
    public void GetDocumentCollection_KeyTypeChanged_QuarantinesAndKeepsEveryCopy()
    {
        using (var database = Open())
        {
            database.GetDocumentCollection<int, DatabaseItem>("items", item => item.Id).Upsert(new DatabaseItem { Id = 1 });
        }

        var reports = new List<string>();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var database = new FileDatabase(_directory, DatabaseProtection.None, (name, exception) => reports.Add(exception.Message));
            database.GetDocumentCollection<long, DatabaseItem>("items", item => item.Id).Upsert(new DatabaseItem { Id = 2 });
            database.Dispose();
            File.Delete(Path.Combine(_directory, "items.db"));
            File.Copy(Path.Combine(_directory, "items.db.corrupt"), Path.Combine(_directory, "items.db"));
        }

        Assert.Equal(2, reports.Count);
        Assert.Contains("key type", reports[0]);
        Assert.True(File.Exists(Path.Combine(_directory, "items.db.corrupt")));
        Assert.True(File.Exists(Path.Combine(_directory, "items.db.corrupt.1")));
    }

    [Fact]
    public void GetDocument_NameDiffersOnlyInCase_Throws()
    {
        using var database = Open();
        database.GetDocument<DatabaseItem>("Player");

        Assert.Throws<InvalidOperationException>(() => database.GetDocument<DatabaseItem>("player"));
    }

    [Fact]
    public void GetDocument_NonAsciiName_Throws()
    {
        using var database = Open();

        Assert.Throws<ArgumentException>(() => database.GetDocument<DatabaseItem>("caf\u00e9"));
    }

    [Fact]
    public void Upsert_StringKeyWithLoneSurrogate_Throws()
    {
        using var database = Open();
        var collection = database.GetDocumentCollection<string, DatabaseNamed>("named", item => item.Name);

        Assert.Throws<ArgumentException>(() => collection.Upsert(new DatabaseNamed { Name = "a\ud800" }));
    }

    [Fact]
    public void Set_Null_StoresANewValueInMemoryAndOnDisk()
    {
        using (var database = Open())
        {
            database.GetDocument<DatabaseItem>("item").Set(null!);

            Assert.NotNull(database.GetDocument<DatabaseItem>("item").Value);
        }

        using var reopened = Open();

        Assert.NotNull(reopened.GetDocument<DatabaseItem>("item").Value);
    }

    private FileDatabase Open()
    {
        return new FileDatabase(_directory);
    }
}

[Schema]
public sealed class DatabaseItem
{
    public int Id;
}

[Schema]
public sealed class DatabaseNamed
{
    public string Name = string.Empty;
}
