using System.IO;
using GroveGames.DependencyInjection;
using GroveGames.Serialization;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public static class DatabaseContainerBuilderExtensions
    {
        public static IContainerBuilder AddDatabase(this IContainerBuilder builder, FormatterRegistry registry)
        {
            return builder.AddDatabase(registry, Path.Combine(Application.persistentDataPath, "Database"));
        }

        public static IContainerBuilder AddDatabase(this IContainerBuilder builder, FormatterRegistry registry, string directory)
        {
            builder.AddSingleton<IDatabase>(_ => new FileDatabase(directory, registry));
            return builder.AddSingleton(resolver => new DatabaseAutoSaveEntryPoint(resolver.Resolve<IDatabase>()));
        }
    }
}
