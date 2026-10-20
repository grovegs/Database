using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GroveGames.Database.Unity.Editor
{
    internal static class DatabaseSettingsAsset
    {
        public const string AssetPath = "Assets/Settings/GroveGames/DatabaseSettings.asset";

        private const string LegacyAssetPath = "Assets/Settings/Resources/GroveGames/DatabaseSettings.asset";

        [InitializeOnLoadMethod]
        private static void MigrateLegacyAsset()
        {
            if (File.Exists(LegacyAssetPath) && !EditorBuildSettings.TryGetConfigObject(DatabaseSettings.ConfigName, out DatabaseSettings _))
            {
                EditorApplication.delayCall += () => GetOrCreate();
            }
        }

        public static DatabaseSettings GetOrCreate()
        {
            if (!EditorBuildSettings.TryGetConfigObject(DatabaseSettings.ConfigName, out DatabaseSettings settings) || settings == null)
            {
                settings = LoadOrCreate();
                EditorBuildSettings.AddConfigObject(DatabaseSettings.ConfigName, settings, true);
            }

            Preload(settings);
            return settings;
        }

        private static DatabaseSettings LoadOrCreate()
        {
            CreateFolder();

            if (AssetDatabase.LoadAssetAtPath<DatabaseSettings>(AssetPath) == null && AssetDatabase.LoadAssetAtPath<DatabaseSettings>(LegacyAssetPath) != null)
            {
                AssetDatabase.MoveAsset(LegacyAssetPath, AssetPath);
            }

            var settings = AssetDatabase.LoadAssetAtPath<DatabaseSettings>(AssetPath);

            if (settings != null)
            {
                return settings;
            }

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DatabaseSettings>(), AssetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<DatabaseSettings>(AssetPath);
        }

        private static void Preload(DatabaseSettings settings)
        {
            var preloaded = PlayerSettings.GetPreloadedAssets();
            var assets = new List<Object>(preloaded.Length + 1);
            var isPreloaded = false;

            for (var i = 0; i < preloaded.Length; i++)
            {
                var asset = preloaded[i];

                if (asset is DatabaseSettings && asset != settings)
                {
                    continue;
                }

                isPreloaded |= asset == settings;
                assets.Add(asset);
            }

            if (!isPreloaded)
            {
                assets.Add(settings);
            }

            if (isPreloaded && assets.Count == preloaded.Length)
            {
                return;
            }

            PlayerSettings.SetPreloadedAssets(assets.ToArray());
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
