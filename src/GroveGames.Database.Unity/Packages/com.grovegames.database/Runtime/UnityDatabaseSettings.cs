using UnityEngine;

namespace GroveGames.Database.Unity
{
    public sealed class UnityDatabaseSettings : ScriptableObject, IDatabaseSettings
    {
        public const string ConfigName = "com.grovegames.database.settings";

        private static UnityDatabaseSettings? s_instance;

        [SerializeField] private string _folderName = "Database";
        [SerializeField] private bool _tamperProtection;

        public string FolderName => _folderName;
        public bool TamperProtection => _tamperProtection;

        public static UnityDatabaseSettings GetOrCreate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorBuildSettings.TryGetConfigObject(ConfigName, out UnityDatabaseSettings settings) && settings != null)
            {
                return settings;
            }
#else
            if (s_instance == null)
            {
                var preloaded = Resources.FindObjectsOfTypeAll<UnityDatabaseSettings>();
                s_instance = preloaded.Length > 0 ? preloaded[0] : null;
            }
#endif
            if (s_instance == null)
            {
                s_instance = CreateInstance<UnityDatabaseSettings>();
                s_instance.hideFlags = HideFlags.DontSave;
            }

            return s_instance;
        }
    }
}
