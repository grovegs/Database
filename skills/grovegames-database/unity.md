## Unity

- Never save player state in `PlayerPrefs`.
- `builder.AddDatabase()` saves when the app loses focus, when it quits and on the `Auto Save Interval` timer, which is why game code does not call `Save()`.
- If a store is moved aside as `.corrupt`, keep the file: it holds the player's data and a fixed build can read it.
