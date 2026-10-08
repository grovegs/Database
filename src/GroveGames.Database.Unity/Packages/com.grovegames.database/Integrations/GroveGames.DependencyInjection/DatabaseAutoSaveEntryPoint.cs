using System;
using GroveGames.DependencyInjection;

namespace GroveGames.Database.Unity
{
    internal sealed class DatabaseAutoSaveEntryPoint : IInitializable, IDisposable
    {
        private readonly IDatabase _database;
        private DatabaseAutoSave? _autoSave;

        public DatabaseAutoSaveEntryPoint(IDatabase database)
        {
            _database = database;
            _autoSave = null;
        }

        public void Initialize()
        {
            _autoSave ??= new DatabaseAutoSave(_database);
        }

        public void Dispose()
        {
            _autoSave?.Dispose();
            _autoSave = null;
        }
    }
}
