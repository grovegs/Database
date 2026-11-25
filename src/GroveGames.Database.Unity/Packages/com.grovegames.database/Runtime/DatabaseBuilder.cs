using System.IO;
using GroveGames.SecureStorage.Unity;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public sealed class DatabaseBuilder : IDatabaseBuilder
    {
        private string _folderName;
        private bool _tamperProtection;

        public DatabaseBuilder(IDatabaseSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("Settings cannot be null");
                settings = ScriptableObject.CreateInstance<UnityDatabaseSettings>();
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
            DatabaseReset.Apply(Directory);
            var protection = _tamperProtection
                ? DatabaseKeyStore.CreateProtection(new UnitySecureStorage())
                : DatabaseProtection.None;
            return new FileDatabase(Directory, protection, static (name, exception) => Debug.LogError($"The '{name}' store could not be read, so it was moved to {name}.db.corrupt and opened empty. {exception}"));
        }
    }
}
