namespace GroveGames.Database.Tests;

public sealed class DatabaseSettingsTests
{
    [Fact]
    public void Constructor_NullFolderName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DatabaseSettings(null!, false));
    }

    [Fact]
    public void Constructor_EmptyFolderName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new DatabaseSettings(string.Empty, false));
    }

    [Fact]
    public void Constructor_ValidArguments_ExposesThem()
    {
        var settings = new DatabaseSettings("Saves", true);

        Assert.Equal("Saves", settings.FolderName);
        Assert.True(settings.TamperProtection);
    }
}
