using System;
using GroveGames.DependencyInjection;

namespace GroveGames.Database.Unity
{
    internal sealed class DatabaseAutoSaveEntryPoint : IInitializable, IFrameUpdatable, IDisposable
    {
        private readonly IDatabase _database;
        private readonly float _interval;
        private DatabaseAutoSave? _autoSave;

        public DatabaseAutoSaveEntryPoint(IDatabase database, float interval)
        {
            _database = database;
            _interval = interval;
            _autoSave = null;
        }

        public void Initialize()
        {
            _autoSave ??= new DatabaseAutoSave(_database, _interval);
        }

        public void FrameUpdate(DeltaTime deltaTime)
        {
            _autoSave?.Tick(deltaTime.Unscaled);
        }

        public void Dispose()
        {
            _autoSave?.Dispose();
            _autoSave = null;
        }
    }
}
