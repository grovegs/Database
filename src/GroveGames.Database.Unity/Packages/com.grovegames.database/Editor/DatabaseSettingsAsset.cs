using UnityEditor;
using UnityEngine;

namespace GroveGames.Database.Unity.Editor
{
    internal static class DatabaseSettingsAsset
    {
        public const string AssetPath = "Assets/Settings/Resources/" + DatabaseSettings.ResourcePath + ".asset";

        public static DatabaseSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DatabaseSettings>(AssetPath);

            if (settings != null)
            {
                return settings;
            }

            CreateFolder();
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DatabaseSettings>(), AssetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<DatabaseSettings>(AssetPath);
        }

        private static void CreateFolder()
        {
            var current = "Assets";

            foreach (var part in AssetPath.Substring("Assets/".Length).Split('/'))
            {
                if (part.EndsWith(".asset", System.StringComparison.Ordinal))
                {
                    return;
                }

                var next = current + "/" + part;

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, part);
                }

                current = next;
            }
        }
    }
}
