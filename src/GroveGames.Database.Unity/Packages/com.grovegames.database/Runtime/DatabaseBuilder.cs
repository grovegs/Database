using System;
using System.IO;
using GroveGames.SecureStorage.Unity;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public sealed class DatabaseBuilder : IDatabaseBuilder
    {
        private const string DefaultFolderName = "Database";

        private string _folderName;
        private bool _tamperProtection;

        public DatabaseBuilder(IDatabaseSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("Settings cannot be null");
                settings = ScriptableObject.CreateInstance<UnityDatabaseSettings>();
            }

            _folderName = IsValidFolderName(settings.FolderName) ? settings.FolderName : DefaultFolderName;
            _tamperProtection = settings.TamperProtection;
        }

        public string Directory => Path.Combine(Application.persistentDataPath, _folderName);
        public bool TamperProtection => _tamperProtection;

        public IDatabaseBuilder SetFolderName(string folderName)
        {
            if (!IsValidFolderName(folderName))
            {
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
            var directory = Directory;
            DatabaseReset.Apply(directory);
            var isKeyStored = false;
            var protection = _tamperProtection
                ? DatabaseKeyStore.CreateProtection(new UnitySecureStorage(), directory, out isKeyStored)
                : DatabaseProtection.None;

            try
            {
                var database = new FileDatabase(directory, protection, static (name, exception) => Debug.LogError($"The '{name}' store could not be read, so it was moved aside as a .corrupt copy and opened empty. {exception}"));

                if (_tamperProtection && isKeyStored)
                {
                    DatabaseKeyStore.DeleteTrustMarker(directory);
                }

                return database;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                var fallback = Path.Combine(Application.temporaryCachePath, _folderName);
                Debug.LogError($"The database folder {directory} could not be opened, so this session saves to {fallback}, which is not kept. {exception}");
                return new FileDatabase(fallback, DatabaseProtection.None);
            }
        }

        private static bool IsValidFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName) || folderName == "." || folderName == ".." || folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || folderName.IndexOf('/') >= 0 || folderName.IndexOf('\\') >= 0)
            {
                Debug.LogError($"The database folder name '{folderName}' must be a single folder name, so '{DefaultFolderName}' is used");
                return false;
            }

            return true;
        }
    }
}
