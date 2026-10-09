using System;
using GroveGames.DependencyInjection;

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
            return builder.AddSingleton(resolver => new DatabaseAutoSaveEntryPoint(resolver.Resolve<IDatabase>()));
        }
    }
}
