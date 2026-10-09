#nullable disable
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GroveGames.Database.Unity.Editor
{
    internal static class DatabaseSettingsAsset
    {
        public const string AssetPath = "Assets/Settings/Resources/" + DatabaseSettings.ResourcePath + ".asset";

        private const string LegacyConfigName = "com.grovegames.database.settings";

        [InitializeOnLoadMethod]
        private static void MigrateOnLoad()
        {
            EditorApplication.delayCall += Migrate;
        }

        public static DatabaseSettings GetOrCreate()
        {
            Migrate();
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

        public static void Migrate()
        {
            if (AssetDatabase.LoadAssetAtPath<DatabaseSettings>(AssetPath) == null)
            {
                var existing = FindExisting();

                if (existing != null)
                {
                    CreateFolder();
                    var error = AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(existing), AssetPath);

                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogError($"Could not move {nameof(DatabaseSettings)} to {AssetPath}: {error}");
                    }
                }
            }

            if (EditorBuildSettings.TryGetConfigObject<DatabaseSettings>(LegacyConfigName, out _))
            {
                EditorBuildSettings.RemoveConfigObject(LegacyConfigName);
            }

            RemoveFromPreloadedAssets();
        }

        private static DatabaseSettings FindExisting()
        {
            if (EditorBuildSettings.TryGetConfigObject<DatabaseSettings>(LegacyConfigName, out var legacy) && legacy != null)
            {
                return legacy;
            }

            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(DatabaseSettings)}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.StartsWith("Assets/", System.StringComparison.Ordinal))
                {
                    return AssetDatabase.LoadAssetAtPath<DatabaseSettings>(path);
                }
            }

            return null;
        }

        private static void CreateFolder()
        {
            var directory = Path.GetDirectoryName(AssetPath).Replace('\\', '/');

            if (AssetDatabase.IsValidFolder(directory))
            {
                return;
            }

            var current = "Assets";

            foreach (var part in directory.Substring("Assets/".Length).Split('/'))
            {
                var next = current + "/" + part;

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, part);
                }

                current = next;
            }
        }

        private static void RemoveFromPreloadedAssets()
        {
            var preloaded = new List<Object>(PlayerSettings.GetPreloadedAssets());

            if (preloaded.RemoveAll(asset => asset is DatabaseSettings) > 0)
            {
                PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
            }
        }
    }
}
