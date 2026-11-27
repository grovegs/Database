using UnityEngine;

namespace GroveGames.Database.Unity
{
    public sealed class UnityDatabaseSettings : ScriptableObject, IDatabaseSettings
    {
        public const string ConfigName = "com.grovegames.database.settings";

        private static UnityDatabaseSettings? s_instance;

        [SerializeField] private string _folderName = "Database";
        [SerializeField] private bool _tamperProtection;
        [SerializeField, Min(0f)] private float _autoSaveInterval = 60f;

        public string FolderName => _folderName;
        public bool TamperProtection => _tamperProtection;
        public float AutoSaveInterval => _autoSaveInterval;

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

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_folderName))
            {
                _folderName = "Database";
            }

            _autoSaveInterval = Mathf.Max(0f, _autoSaveInterval);
        }
    }
}
