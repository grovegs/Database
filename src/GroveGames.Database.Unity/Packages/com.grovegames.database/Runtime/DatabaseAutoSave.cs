using System;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public sealed class DatabaseAutoSave : IDisposable
    {
        private readonly IDatabase? _database;
        private readonly float _interval;
        private float _elapsed;
        private bool _isFocused;
        private bool _disposed;

        public DatabaseAutoSave(IDatabase database) : this(database, 0f)
        {
        }

        public DatabaseAutoSave(IDatabase database, float interval)
        {
            if (database == null)
            {
                Debug.LogError("The database auto-save has no database, so it saves nothing");
            }

            _database = database;
            _interval = float.IsNaN(interval) || interval < 0f ? 0f : interval;
            _elapsed = 0f;
            _isFocused = true;
            _disposed = false;
            Application.focusChanged += HandleFocusChanged;
            Application.quitting += HandleQuitting;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Application.focusChanged -= HandleFocusChanged;
            Application.quitting -= HandleQuitting;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (_disposed || _interval <= 0f || !_isFocused)
            {
                return;
            }

            _elapsed += unscaledDeltaTime;

            if (_elapsed < _interval)
            {
                return;
            }

            Save();
        }

        private void HandleFocusChanged(bool isFocused)
        {
            _isFocused = isFocused;

            if (!_disposed && !isFocused)
            {
                Save();
            }
        }

        private void HandleQuitting()
        {
            if (!_disposed)
            {
                Save();
            }
        }

        // An exception here would stop the other handlers of the Unity event it runs from.
        private void Save()
        {
            _elapsed = 0f;

            if (_database == null)
            {
                return;
            }

            try
            {
                _database.Save();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
