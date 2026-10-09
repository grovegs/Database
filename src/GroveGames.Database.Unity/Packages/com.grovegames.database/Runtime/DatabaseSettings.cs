using UnityEngine;

namespace GroveGames.Database.Unity
{
    public sealed class DatabaseSettings : ScriptableObject
    {
        public const string ResourcePath = "GroveGames/DatabaseSettings";

        [SerializeField] private string _folderName = "Database";
        [SerializeField] private bool _tamperProtection;

        public string FolderName => _folderName;
        public bool TamperProtection => _tamperProtection;

        public static DatabaseSettings GetOrCreate()
        {
            var settings = Resources.Load<DatabaseSettings>(ResourcePath);
            return settings != null ? settings : CreateInstance<DatabaseSettings>();
        }
    }
}
