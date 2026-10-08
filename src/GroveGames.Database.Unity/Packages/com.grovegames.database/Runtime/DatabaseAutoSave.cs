using System;
using UnityEngine;

namespace GroveGames.Database.Unity
{
    public sealed class DatabaseAutoSave : IDisposable
    {
        private readonly IDatabase _database;
        private bool _disposed;

        public DatabaseAutoSave(IDatabase database)
        {
            _database = database;
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

        private void HandleFocusChanged(bool isFocused)
        {
            if (!isFocused)
            {
                _database.Save();
            }
        }

        private void HandleQuitting()
        {
            _database.Save();
        }
    }
}
