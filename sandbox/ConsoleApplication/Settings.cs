using GroveGames.Serialization;

namespace ConsoleApplication;

[Schema]
public sealed class Settings
{
    public float Volume = 1f;
    public string? Language;
}
