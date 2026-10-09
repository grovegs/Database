using GroveGames.Database.Storage;

namespace GroveGames.Database;

public abstract class DatabaseProtection
{
    private const int MinimumKeyLength = 16;

    private DatabaseProtection()
    {
    }

    public static DatabaseProtection None { get; } = new NoProtection();

    public static DatabaseProtection TamperCheck(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length < MinimumKeyLength)
        {
            throw new ArgumentException($"The key must be at least {MinimumKeyLength} bytes.", nameof(key));
        }

        return new TamperCheckProtection((byte[])key.Clone());
    }

    internal abstract IEntrySigner CreateSigner(string storeName);

    private sealed class NoProtection : DatabaseProtection
    {
        internal override IEntrySigner CreateSigner(string storeName)
        {
            return NoEntrySigner.Instance;
        }
    }

    private sealed class TamperCheckProtection : DatabaseProtection
    {
        private readonly byte[] _key;

        public TamperCheckProtection(byte[] key)
        {
            _key = key;
        }

        internal override IEntrySigner CreateSigner(string storeName)
        {
            return new HmacEntrySigner(_key, storeName);
        }
    }
}
