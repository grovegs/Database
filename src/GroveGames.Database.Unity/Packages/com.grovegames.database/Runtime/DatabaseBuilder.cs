using System.IO;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public sealed class DatabaseBuilder : IDatabaseBuilder
    {
        private string _folderName;
        private bool _tamperProtection;

        public DatabaseBuilder(DatabaseSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("DatabaseSettings cannot be null");
                settings = ScriptableObject.CreateInstance<DatabaseSettings>();
            }

            _folderName = settings.FolderName;
            _tamperProtection = settings.TamperProtection;
        }

        public string Directory => Path.Combine(Application.persistentDataPath, _folderName);
        public bool TamperProtection => _tamperProtection;

        public IDatabaseBuilder SetFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                Debug.LogError("The database folder name cannot be empty");
                return this;
            }

            _folderName = folderName;
            return this;
        }

        public IDatabaseBuilder SetTamperProtection(bool enabled)
        {
            _tamperProtection = enabled;
            return this;
        }

        public IDatabase Build()
        {
            var protection = _tamperProtection
                ? DatabaseKeyStore.CreateProtection(new global::GroveGames.SecureStorage.Unity.SecureStorage())
                : DatabaseProtection.None;
            return new FileDatabase(Directory, protection);
        }
    }
}
