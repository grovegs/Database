using BenchmarkDotNet.Attributes;
using GroveGames.Database;
using GroveGames.Serialization;

namespace DotnetBenchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class Benchmark
{
    private string _directory = null!;
    private FileDatabase _database = null!;
    private IDocumentCollection<int, Player> _players = null!;
    private IDocument<Player> _profile = null!;
    private Player _player = null!;

    [GlobalSetup]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "GroveGames.Database.Benchmark", Guid.NewGuid().ToString("N"));
        _database = new FileDatabase(_directory, new FormatterRegistryBuilder().AddDotnetBenchmarkFormatters().Build());
        _players = _database.GetDocumentCollection<int, Player>("players", player => player.Id);
        _profile = _database.GetDocument<Player>("profile");
        _player = new Player { Id = 1, Name = "Hero", Level = 12, Gold = 4500, Experience = 0.5f };
        _players.Upsert(_player);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _database.Dispose();
        Directory.Delete(_directory, true);
    }

    [Benchmark]
    public void Upsert()
    {
        _player.Gold++;
        _players.Upsert(_player);
    }

    [Benchmark]
    public void DocumentSet()
    {
        _player.Gold++;
        _profile.Set(_player);
    }

    [Benchmark]
    public Player Get()
    {
        return _players.Get(1);
    }

    [Benchmark]
    public void UpsertAndSave()
    {
        _player.Gold++;
        _players.Upsert(_player);
        _database.Save();
    }
}
