using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GroveGames.Database.Unity.Editor
{
    internal static class DatabaseSettingsProvider
    {
        private const string AssetPath = "Assets/Settings/DatabaseSettings.asset";

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/GroveGames/Database", SettingsScope.Project)
            {
                label = "Database",
                activateHandler = (searchContext, rootElement) =>
                {
                    var settings = GetCurrentSettings();
                    var serializedObject = new SerializedObject(settings);

                    var container = new VisualElement
                    {
                        style =
                        {
                            paddingLeft = 10,
                            paddingRight = 10,
                            paddingTop = 10,
                            paddingBottom = 10
                        }
                    };

                    var title = new Label("Database Settings")
                    {
                        style =
                        {
                            fontSize = 19,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            marginBottom = 10
                        }
                    };
                    container.Add(title);

                    var assetField = new ObjectField("Settings Asset")
                    {
                        objectType = typeof(DatabaseSettings),
                        value = settings,
                        style = { marginBottom = 10 }
                    };
                    assetField.RegisterValueChangedCallback(evt =>
                    {
                        if (evt.newValue is DatabaseSettings newSettings)
                        {
                            EditorBuildSettings.AddConfigObject(DatabaseSettings.GetConfigName(), newSettings, true);
                            AddToPreloadedAssets(newSettings);
                            serializedObject.Dispose();
                            serializedObject = new SerializedObject(newSettings);
                            rootElement.Bind(serializedObject);
                        }
                    });
                    container.Add(assetField);

                    container.Add(new PropertyField(serializedObject.FindProperty("_folderName"), "Folder Name"));
                    container.Add(new PropertyField(serializedObject.FindProperty("_tamperProtection"), "Tamper Protection"));
                    container.Add(new HelpBox("Tamper Protection rejects saves edited outside the game. It needs an IDatabaseKey registered in the root installer.", HelpBoxMessageType.Info));

                    rootElement.Add(container);
                    rootElement.Bind(serializedObject);
                },
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "Database", "Save", "Folder", "Tamper", "Protection", "Grove Games" })
            };
        }

        private static DatabaseSettings GetCurrentSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject<DatabaseSettings>(DatabaseSettings.GetConfigName(), out var existingSettings))
            {
                if (existingSettings != null)
                {
                    AddToPreloadedAssets(existingSettings);
                    return existingSettings;
                }
            }

            var settings = AssetDatabase.LoadAssetAtPath<DatabaseSettings>(AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DatabaseSettings>();

                var directory = System.IO.Path.GetDirectoryName(AssetPath);
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
            }

            EditorBuildSettings.AddConfigObject(DatabaseSettings.GetConfigName(), settings, true);
            AddToPreloadedAssets(settings);
            return settings;
        }

        internal static void AddToPreloadedAssets(DatabaseSettings settings)
        {
            var preloadedAssets = new System.Collections.Generic.List<Object>(PlayerSettings.GetPreloadedAssets());

            if (preloadedAssets.Contains(settings))
            {
                return;
            }

            preloadedAssets.RemoveAll(asset => asset is DatabaseSettings);
            preloadedAssets.Add(settings);
            PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
        }
    }
}
