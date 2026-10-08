namespace GroveGames.Database.Storage;

internal enum EntryKind : byte
{
    Value = 1,
    Upsert = 2,
    Remove = 3,
    Clear = 4
}
