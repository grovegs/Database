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
        return TamperCheck(key, false);
    }

    public static DatabaseProtection TamperCheck(byte[] key, bool trustExistingFiles)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length < MinimumKeyLength)
        {
            throw new ArgumentException($"The key must be at least {MinimumKeyLength} bytes.", nameof(key));
        }

        return new TamperCheckProtection((byte[])key.Clone(), trustExistingFiles);
    }

    internal abstract bool TrustsExistingFiles { get; }

    internal abstract DatabaseProtection WithoutTrust();

    internal abstract IEntrySigner CreateSigner(string storeName);

    private sealed class NoProtection : DatabaseProtection
    {
        internal override bool TrustsExistingFiles => false;

        internal override DatabaseProtection WithoutTrust()
        {
            return this;
        }

        internal override IEntrySigner CreateSigner(string storeName)
        {
            return NoEntrySigner.Instance;
        }
    }

    private sealed class TamperCheckProtection : DatabaseProtection
    {
        private readonly byte[] _key;
        private readonly bool _trustExistingFiles;

        public TamperCheckProtection(byte[] key, bool trustExistingFiles)
        {
            _key = key;
            _trustExistingFiles = trustExistingFiles;
        }

        internal override bool TrustsExistingFiles => _trustExistingFiles;

        internal override DatabaseProtection WithoutTrust()
        {
            return _trustExistingFiles ? new TamperCheckProtection(_key, false) : this;
        }

        internal override IEntrySigner CreateSigner(string storeName)
        {
            return new HmacEntrySigner(_key, storeName, _trustExistingFiles);
        }
    }
}
