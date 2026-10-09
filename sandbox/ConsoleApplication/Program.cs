using GroveGames.Database;
using GroveGames.Serialization;

namespace ConsoleApplication;

public static class Program
{
    public static void Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GroveGames.Database.Sample");

        using (var database = new FileDatabase(directory))
        {
            var players = database.GetDocumentCollection<int, Player>("players", player => player.Id);
            var settings = database.GetDocument<Settings>("settings");

            var hero = players.TryGet(1, out var existing) ? existing : new Player { Id = 1, Name = "Hero" };
            hero.Gold += 100;
            players.Upsert(hero);

            var current = settings.Value;
            current.Language ??= "en";
            settings.Set(current);

            database.Save();
            Console.WriteLine($"{hero.Name} has {hero.Gold} gold, language {current.Language}, {players.Count} player(s) stored in {directory}");
        }
    }
}
