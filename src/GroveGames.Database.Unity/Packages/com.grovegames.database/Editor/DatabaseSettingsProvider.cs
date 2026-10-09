using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GroveGames.Database.Unity.Editor
{
    internal static class DatabaseSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/GroveGames/Database", SettingsScope.Project)
            {
                label = "Database",
                activateHandler = (searchContext, rootElement) =>
                {
                    var settings = DatabaseSettingsAsset.GetOrCreate();
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

                    container.Add(new Label("Database Settings")
                    {
                        style =
                        {
                            fontSize = 19,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            marginBottom = 10
                        }
                    });

                    var assetField = new ObjectField("Settings Asset")
                    {
                        objectType = typeof(DatabaseSettings),
                        value = settings,
                        style = { marginBottom = 10 }
                    };
                    assetField.SetEnabled(false);
                    container.Add(assetField);

                    container.Add(new PropertyField(serializedObject.FindProperty("_folderName"), "Folder Name"));
                    container.Add(new PropertyField(serializedObject.FindProperty("_tamperProtection"), "Tamper Protection"));
                    container.Add(new HelpBox("Tamper Protection rejects saves edited outside the game. It needs an IDatabaseKey registered in the root installer.", HelpBoxMessageType.Info));

                    rootElement.Add(container);
                    rootElement.Bind(serializedObject);
                },
                keywords = new HashSet<string>(new[] { "Database", "Save", "Folder", "Tamper", "Protection", "Grove Games" })
            };
        }
    }
}
