using System.IO;
using GroveGames.DependencyInjection;
using GroveGames.Serialization;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public static class DatabaseContainerBuilderExtensions
    {
        public static IContainerBuilder AddDatabase(this IContainerBuilder builder)
        {
            return builder.AddDatabase(Path.Combine(Application.persistentDataPath, "Database"));
        }

        public static IContainerBuilder AddDatabase(this IContainerBuilder builder, string directory)
        {
            builder.AddSingleton<IDatabase>(resolver => new FileDatabase(directory, resolver.Resolve<FormatterRegistry>()));
            return builder.AddSingleton(resolver => new DatabaseAutoSaveEntryPoint(resolver.Resolve<IDatabase>()));
        }
    }
}
