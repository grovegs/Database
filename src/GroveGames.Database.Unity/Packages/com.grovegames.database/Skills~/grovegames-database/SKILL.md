---
name: grovegames-database
description: Rules for saving player state in game code with GroveGames.Database, covering record documents, repositories, schema versions and migrations. Use when adding or changing saved data, a Schema class, a record repository or a migration, or when code touches IDatabase or PlayerPrefs.
---

# Using GroveGames.Database

Rules for game code that saves player state with this package. They hold in every game. The API is in the package's `README.md`.

- Save player state with this package only.
- A piece of player state is a `[Schema]` class named `XRecordDocument`, opened with `database.GetDocument<XRecordDocument>("x")`.
- For a keyed set, use `GetDocumentCollection<TKey, XRecordDocument>`.
- Wrap each document in an `IXRecordRepository` with `GetX` and `SetX` methods. Services read and write through the repository, never through `IDatabase`.
- A change to a `[Schema]` class that an existing save cannot load needs `[Schema(version: N)]` and an `IMigration<T>`. Say what happens to an existing save before making the change.
- Do not call `Save()` from game code. A change survives the app crashing or being killed as soon as `Set`, `Upsert` or `Remove` returns. `Save()` only adds protection against a power cut or an operating system crash.
- Never edit the files in the database folder by hand. Each record carries a checksum.

## Unity

- Never save player state in `PlayerPrefs`.
- `builder.AddDatabase()` saves when the app loses focus, when it quits and on the `Auto Save Interval` timer, which is why game code does not call `Save()`.
- If a store is moved aside as `.corrupt`, keep the file: it holds the player's data and a fixed build can read it.
