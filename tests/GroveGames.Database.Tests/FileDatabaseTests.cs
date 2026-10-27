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
