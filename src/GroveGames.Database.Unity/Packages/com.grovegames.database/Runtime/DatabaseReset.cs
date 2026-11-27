using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public static class DatabaseReset
    {
        public const string AllStores = "*";

        private const string MarkerExtension = ".reset";
        private const string StoreExtension = ".db";

        public static void Schedule(string directory, string name)
        {
            var names = new List<string>();
            GetScheduled(directory, names);

            if (names.Contains(name) || names.Contains(AllStores))
            {
                return;
            }

            names.Add(name);
            Write(directory, names);
        }

        public static void Unschedule(string directory, string name)
        {
            var names = new List<string>();
            GetScheduled(directory, names);

            if (!names.Remove(name))
            {
                return;
            }

            Write(directory, names);
        }

        public static void GetScheduled(string directory, List<string> names)
        {
            names.Clear();
            var marker = GetMarkerPath(directory);

            try
            {
                if (!File.Exists(marker))
                {
                    return;
                }

                foreach (var line in File.ReadAllLines(marker))
                {
                    var name = line.Trim();

                    if (name.Length > 0 && !names.Contains(name))
                    {
                        names.Add(name);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to read the database reset marker: {exception.Message}");
            }
        }

        public static void Apply(string directory)
        {
            var names = new List<string>();
            GetScheduled(directory, names);

            if (names.Count == 0)
            {
                return;
            }

            // The marker goes first, so a reset that fails partway is not applied again over the next session's progress.
            try
            {
                File.Delete(GetMarkerPath(directory));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to delete the database reset marker, so the reset is skipped: {exception.Message}");
                return;
            }

            try
            {
                if (names.Contains(AllStores))
                {
                    if (IsPersistentDataPath(directory))
                    {
                        Debug.LogError($"Refusing to delete {directory}, because it is the app's whole data folder rather than a database folder");
                    }
                    else if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
                else
                {
                    foreach (var name in names)
                    {
                        var path = Path.Combine(directory, name + StoreExtension);

                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to reset the database: {exception.Message}");
            }
        }

        private static void Write(string directory, List<string> names)
        {
            var marker = GetMarkerPath(directory);

            try
            {
                if (names.Count == 0)
                {
                    File.Delete(marker);
                    return;
                }

                File.WriteAllLines(marker, names);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to write the database reset marker: {exception.Message}");
            }
        }

        private static bool IsPersistentDataPath(string directory)
        {
            var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            return string.Equals(Path.GetFullPath(directory).TrimEnd(separators), Path.GetFullPath(Application.persistentDataPath).TrimEnd(separators), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetMarkerPath(string directory)
        {
            return directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + MarkerExtension;
        }
    }
}
