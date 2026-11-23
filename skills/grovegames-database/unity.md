## Unity

- Never save player state in `PlayerPrefs`.
- `builder.AddDatabase()` saves when the app loses focus and when it quits, which is why game code does not call `Save()`.
