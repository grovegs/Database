using GroveGames.Serialization;

namespace DotnetBenchmark;

[Schema]
public sealed class Player
{
    public int Id;
    public string? Name;
    public int Level;
    public long Gold;
    public float Experience;
}
