using System;
using GroveGames.DependencyInjection;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public static class DatabaseContainerBuilderExtensions
    {
        public static IContainerBuilder AddDatabase(this IContainerBuilder builder)
        {
            return builder.AddDatabase(_ => { });
        }

        public static IContainerBuilder AddDatabase(this IContainerBuilder builder, Action<IDatabaseBuilder> configure)
        {
            builder.AddSingleton<IDatabase>(resolver =>
            {
                var databaseBuilder = new DatabaseBuilder(DatabaseSettings.GetOrCreate());
                configure(databaseBuilder);
                return new FileDatabase(databaseBuilder.Directory, CreateProtection(databaseBuilder, resolver));
            });
            return builder.AddSingleton(resolver => new DatabaseAutoSaveEntryPoint(resolver.Resolve<IDatabase>()));
        }

        private static DatabaseProtection CreateProtection(DatabaseBuilder databaseBuilder, IObjectResolver resolver)
        {
            if (!databaseBuilder.TamperProtection)
            {
                return DatabaseProtection.None;
            }

            if (resolver.TryResolve<IDatabaseKey>(out var key))
            {
                return DatabaseProtection.TamperCheck(key.Value);
            }

            Debug.LogError("Tamper protection is on, but no IDatabaseKey is registered. Register one in the root installer or turn tamper protection off. The database is opened without protection.");
            return DatabaseProtection.None;
        }
    }
}
