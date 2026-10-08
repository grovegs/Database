# GroveGames.Database

Fast, crash-safe local document database for games on .NET, Unity and Godot, built on [GroveGames.Serialization](https://github.com/grovegs/Serialization).

[![Build Status](https://github.com/grovegs/Database/actions/workflows/release.yml/badge.svg)](https://github.com/grovegs/Database/actions/workflows/release.yml)
[![Tests](https://github.com/grovegs/Database/actions/workflows/tests.yml/badge.svg)](https://github.com/grovegs/Database/actions/workflows/tests.yml)
[![Latest Release](https://img.shields.io/github/v/release/grovegs/Database)](https://github.com/grovegs/Database/releases/latest)
[![NuGet](https://img.shields.io/nuget/v/GroveGames.Database)](https://www.nuget.org/packages/GroveGames.Database)

---

## Features

- **Writes in microseconds**: Every change reaches the operating system before the call returns, in about 1.5 µs with no allocation, so you can write several times per frame.
- **Crash safe**: A change survives the app crashing or being killed as soon as the call returns. A torn or corrupt last entry is detected by its checksum and discarded on the next open, so a save never loads half written.
- **Documents and collections**: Single values such as settings, and keyed collections such as players or inventory, like documents and collections in MongoDB.
- **Versioned migrations**: Records are stored with GroveGames.Serialization, so older records are migrated when they are loaded.
- **Simple storage**: One append-only file per document or collection, compacted when it is opened.

## .NET

```bash
dotnet add package GroveGames.Database
```

### Usage

Mark the stored types with `[Schema]` and open a database with their formatters:

```csharp
[Schema]
public sealed class Player
{
    public int Id;
    public string? Name;
    public long Gold;
}

var registry = new FormatterRegistryBuilder().AddGameFormatters().Build();
using var database = new FileDatabase(path, registry);

IDocumentCollection<int, Player> players = database.GetDocumentCollection<int, Player>("players", player => player.Id);
players.Upsert(player);
players.Remove(id);
bool found = players.TryGet(id, out var existing);

foreach (var each in players)
{
}

IDocument<Settings> settings = database.GetDocument<Settings>("settings");
var current = settings.Value;
settings.Set(current);

database.Save();
```

- **`GetDocumentCollection`** opens a collection. Keys can be `int`, `long`, `string` or `Guid`, and the key selector picks the key from each value.
- **`GetDocument`** opens a single value. `Value` is a new `T` until something is stored.
- **Changed in place:** after changing an object you already stored, call `Upsert` or `Set` with it again to store the change.
- **Opening a name twice** returns the same instance.
- **Not thread-safe:** use a database from one thread.

### Durability

| Event                                     | What is kept                                                                       |
| ----------------------------------------- | ---------------------------------------------------------------------------------- |
| The call to `Upsert`, `Remove` or `Set` returns | The change survives the app crashing, being force-quit or being killed by the OS |
| `Save()` returns                          | Every change is on storage and also survives a power cut or OS crash             |
| Crash in the middle of a write            | Only that write is lost; the torn entry is discarded when the file is next opened |

`Save()` syncs every open file to storage and takes a few milliseconds, so call it at safe points such as pausing, quitting or after a purchase, not after every write.

### Storage

Each document or collection is one file, `{name}.db`, in the database directory:

- **Format:** an 8-byte header, then one entry per change: `[length][kind][payload][CRC32]`. The payload holds the key and the value in MessagePack.
- **Opening:** a file is read once, keeping the latest value for each key, and everything is held in memory.
- **Compaction:** if most of a file is overwritten or removed entries, it is rewritten into a temporary file, synced, and swapped in atomically. A leftover temporary file from an interrupted compaction is removed on the next open.
- **Migrations:** values written by an older version of a type are migrated when the file is opened, and the file is then rewritten the same way with the migrated values, so each value is migrated only once.
- **New files:** creating or replacing a file also syncs its folder, so a new file can't disappear after a power cut.

## Unity

Install the core through [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) (`GroveGames.Database`), then add the package to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.grovegames.database": "https://github.com/grovegs/Database.git?path=src/GroveGames.Database.Unity/Packages/com.grovegames.database"
  }
}
```

### Saving Automatically

`DatabaseAutoSave` calls `Save()` whenever the app loses focus and when it quits. On iOS and Android the app loses focus as it goes to the background, so changes are synced before the OS can kill it:

```csharp
var database = new FileDatabase(Path.Combine(Application.persistentDataPath, "Database"), registry);
var autoSave = new DatabaseAutoSave(database);
```

Dispose `autoSave` before disposing the database.

### Dependency Injection

With [GroveGames.DependencyInjection](https://github.com/grovegs/DependencyInjection) installed, register the database in your root installer with the registry of your stored types:

```csharp
var registry = new FormatterRegistryBuilder()
    .AddUnityFormatters()
    .AddGameFormatters()
    .Build();

builder.AddDatabase(registry);
```

`AddDatabase(registry)` stores files in `Application.persistentDataPath/Database`; `AddDatabase(registry, directory)` takes another folder. It registers `IDatabase` and saves automatically for as long as the container lives, and disposing the container disposes the database. Register the serializers with `AddSerialization(registry)` only if you also use them directly.

## Godot

Download the Godot addon from the [latest release](https://github.com/grovegs/Database/releases/latest) and extract it to your project's `addons` folder. Use a `user://` path converted with `ProjectSettings.GlobalizePath` as the database directory.

## Testing

```bash
dotnet test
```

---

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Write tests for new functionality
4. Submit a pull request

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
