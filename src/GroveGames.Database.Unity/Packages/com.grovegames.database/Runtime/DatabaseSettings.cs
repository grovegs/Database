using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GroveGames.Database.Unity
{
    public sealed class DatabaseSettings : ScriptableObject
    {
        private const string ConfigName = "com.grovegames.database.settings";

        private static DatabaseSettings s_loaded;

        [SerializeField] private string _folderName = "Database";

        public string FolderName => _folderName;

        private void OnEnable()
        {
            s_loaded = this;
        }

        public static DatabaseSettings GetOrCreate()
        {
#if UNITY_EDITOR
            if (EditorBuildSettings.TryGetConfigObject<DatabaseSettings>(ConfigName, out var settings) && settings != null)
            {
                return settings;
            }
#else
            if (s_loaded != null)
            {
                return s_loaded;
            }
#endif
            var defaultSettings = CreateInstance<DatabaseSettings>();
            defaultSettings.name = ConfigName;
            return defaultSettings;
        }

        public static string GetConfigName() => ConfigName;
    }
}
