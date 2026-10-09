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
