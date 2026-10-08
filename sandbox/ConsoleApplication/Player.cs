using GroveGames.Serialization;

namespace ConsoleApplication;

[Schema]
public sealed class Player
{
    public int Id;
    public string? Name;
    public long Gold;
}
