using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GroveGames.Database.Unity.Editor.Tests
{
    public sealed class DatabaseAutoSaveTests
    {
        private static readonly MethodInfo s_invokeFocusChanged = typeof(Application).GetMethod("InvokeFocusChanged", BindingFlags.NonPublic | BindingFlags.Static)!;

        [Test]
        public void FocusLost_SavesDatabase()
        {
            var database = new CountingDatabase();

            using (new DatabaseAutoSave(database))
            {
                s_invokeFocusChanged.Invoke(null, new object[] { false });
            }

            Assert.That(database.Saves, Is.EqualTo(1));
        }

        [Test]
        public void FocusGained_DoesNotSave()
        {
            var database = new CountingDatabase();

            using (new DatabaseAutoSave(database))
            {
                s_invokeFocusChanged.Invoke(null, new object[] { true });
            }

            Assert.That(database.Saves, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_StopsSaving()
        {
            var database = new CountingDatabase();
            new DatabaseAutoSave(database).Dispose();

            s_invokeFocusChanged.Invoke(null, new object[] { false });

            Assert.That(database.Saves, Is.EqualTo(0));
        }

        [Test]
        public void DisposedDuringFocusLost_DoesNotSave()
        {
            var database = new CountingDatabase();
            DatabaseAutoSave? autoSave = null;
            Action<bool> disposeFirst = _ => autoSave?.Dispose();
            Application.focusChanged += disposeFirst;
            autoSave = new DatabaseAutoSave(database);

            try
            {
                s_invokeFocusChanged.Invoke(null, new object[] { false });
            }
            finally
            {
                Application.focusChanged -= disposeFirst;
                autoSave.Dispose();
            }

            Assert.That(database.Saves, Is.EqualTo(0));
        }

        private sealed class CountingDatabase : IDatabase
        {
            public int Saves { get; private set; }

            public IDocument<T> GetDocument<T>(string name) where T : new()
            {
                throw new NotSupportedException();
            }

            public IDocumentCollection<TKey, T> GetDocumentCollection<TKey, T>(string name, Func<T, TKey> keySelector) where TKey : notnull
            {
                throw new NotSupportedException();
            }

            public void Save()
            {
                Saves++;
            }

            public void Dispose()
            {
            }
        }
    }
}
