using UnityEngine;

namespace GroveGames.Database.Unity
{
    public sealed class DatabaseSettings : ScriptableObject
    {
        public const string ConfigName = "com.grovegames.database.settings";

        private static DatabaseSettings? s_instance;

        [SerializeField] private string _folderName = "Database";
        [SerializeField] private bool _tamperProtection;

        public string FolderName => _folderName;
        public bool TamperProtection => _tamperProtection;

        public static DatabaseSettings GetOrCreate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorBuildSettings.TryGetConfigObject(ConfigName, out DatabaseSettings settings) && settings != null)
            {
                return settings;
            }
#else
            if (s_instance == null)
            {
                var preloaded = Resources.FindObjectsOfTypeAll<DatabaseSettings>();
                s_instance = preloaded.Length > 0 ? preloaded[0] : null;
            }
#endif
            if (s_instance == null)
            {
                s_instance = CreateInstance<DatabaseSettings>();
                s_instance.hideFlags = HideFlags.DontSave;
            }

            return s_instance;
        }
    }
}
