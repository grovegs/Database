using GroveGames.DependencyInjection;
using GroveGames.DeveloperTools.Unity;

namespace GroveGames.Database.Unity
{
    public static class DatabaseDeveloperToolsContainerBuilderExtensions
    {
        public static IContainerBuilder AddDatabasePage(this IContainerBuilder builder)
        {
            return builder.AddDeveloperToolsPage<DatabasePage>();
        }
    }
}
