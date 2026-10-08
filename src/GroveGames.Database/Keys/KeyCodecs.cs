namespace GroveGames.Database.Keys;

internal static class KeyCodecs
{
    public static IKeyCodec<TKey> Create<TKey>()
    {
        object? codec = typeof(TKey) == typeof(int) ? new Int32KeyCodec()
            : typeof(TKey) == typeof(long) ? new Int64KeyCodec()
            : typeof(TKey) == typeof(string) ? new StringKeyCodec()
            : typeof(TKey) == typeof(Guid) ? new GuidKeyCodec()
            : null;

        return codec as IKeyCodec<TKey> ?? throw new NotSupportedException($"{typeof(TKey)} keys are not supported. Use int, long, string or Guid.");
    }
}
