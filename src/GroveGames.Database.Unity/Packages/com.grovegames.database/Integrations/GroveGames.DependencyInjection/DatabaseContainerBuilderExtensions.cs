using System.IO;
using GroveGames.DependencyInjection;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public static class DatabaseContainerBuilderExtensions
    {
        public static IContainerBuilder AddDatabase(this IContainerBuilder builder)
        {
            builder.AddSingleton<IDatabase>(resolver => new FileDatabase(
                Path.Combine(Application.persistentDataPath, DatabaseSettings.GetOrCreate().FolderName),
                resolver.TryResolve<IDatabaseKey>(out var key) ? key.Value : null));
            return builder.AddSingleton(resolver => new DatabaseAutoSaveEntryPoint(resolver.Resolve<IDatabase>()));
        }
    }
}
