using System.IO;
using GroveGames.DependencyInjection;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public static class DatabaseContainerBuilderExtensions
    {
        public static IContainerBuilder AddDatabase(this IContainerBuilder builder)
        {
            builder.AddSingleton<IDatabase>(resolver =>
            {
                var settings = DatabaseSettings.GetOrCreate();
                var directory = Path.Combine(Application.persistentDataPath, settings.FolderName);
                return new FileDatabase(directory, CreateProtection(settings, resolver));
            });
            return builder.AddSingleton(resolver => new DatabaseAutoSaveEntryPoint(resolver.Resolve<IDatabase>()));
        }

        private static DatabaseProtection CreateProtection(DatabaseSettings settings, IObjectResolver resolver)
        {
            if (!settings.TamperProtection)
            {
                return DatabaseProtection.None;
            }

            if (resolver.TryResolve<IDatabaseKey>(out var key))
            {
                return DatabaseProtection.TamperCheck(key.Value);
            }

            Debug.LogError("Tamper Protection is on in Project Settings > GroveGames > Database, but no IDatabaseKey is registered. Register one in the root installer or turn Tamper Protection off. The database is opened without protection.");
            return DatabaseProtection.None;
        }
    }
}
