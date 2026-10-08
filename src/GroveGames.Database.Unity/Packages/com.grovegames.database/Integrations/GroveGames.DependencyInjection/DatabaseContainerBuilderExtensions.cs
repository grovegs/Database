using System.IO;
using GroveGames.DependencyInjection;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public static class DatabaseContainerBuilderExtensions
    {
        public static IContainerBuilder AddDatabase(this IContainerBuilder builder)
        {
            builder.AddSingleton<IDatabase>(_ => new FileDatabase(Path.Combine(Application.persistentDataPath, DatabaseSettings.GetOrCreate().FolderName)));
            return builder.AddSingleton(resolver => new DatabaseAutoSaveEntryPoint(resolver.Resolve<IDatabase>()));
        }
    }
}
