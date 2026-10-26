using System;
using GroveGames.DependencyInjection;
#if GROVEGAMES_DEVELOPERTOOLS
using GroveGames.DeveloperTools.Unity;
#endif

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
            builder.AddSingleton<IDatabase>(_ =>
            {
                var databaseBuilder = new DatabaseBuilder(DatabaseSettings.GetOrCreate());
                configure(databaseBuilder);
                return databaseBuilder.Build();
            });
#if GROVEGAMES_DEVELOPERTOOLS
            builder.AddDeveloperToolsPage<DatabasePage>();
#endif
            return builder.AddSingleton(resolver => new DatabaseAutoSaveEntryPoint(resolver.Resolve<IDatabase>()));
        }
    }
}
