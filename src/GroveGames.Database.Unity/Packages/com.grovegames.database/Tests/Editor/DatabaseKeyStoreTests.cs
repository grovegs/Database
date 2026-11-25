using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GroveGames.SecureStorage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GroveGames.Database.Unity.Editor.Tests
{
    public sealed class DatabaseKeyStoreTests
    {
        private string _directory = null!;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "GroveGames.Database.Unity.Tests", Path.GetRandomFileName());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void CreateProtection_KeyStored_ReadsItWithoutWriting()
        {
            var storage = new FakeSecureStorage();
            DatabaseKeyStore.CreateProtection(storage);
            var writes = storage.WriteCount;

            DatabaseKeyStore.CreateProtection(storage);

            Assert.That(writes, Is.EqualTo(1));
            Assert.That(storage.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateProtection_ReadFailsOnce_RetriesAndKeepsTheKey()
        {
            var storage = new FakeSecureStorage();
            DatabaseKeyStore.CreateProtection(storage);
            storage.FailedReads = 1;

            DatabaseKeyStore.CreateProtection(storage);

            Assert.That(storage.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateProtection_ReadKeepsFailing_ReplacesTheKeyAndKeepsTheSaves()
        {
            var storage = new FakeSecureStorage();
            Save(DatabaseKeyStore.CreateProtection(storage), 7);
            storage.FailedReads = 2;
            LogAssert.Expect(LogType.Warning, new Regex("could not be read"));

            var protection = DatabaseKeyStore.CreateProtection(storage);

            Assert.That(storage.WriteCount, Is.EqualTo(2));
            Assert.That(Load(protection), Is.EqualTo(7));
        }

        [Test]
        public void CreateProtection_KeyCannotBeStored_KeepsTheSaves()
        {
            var storage = new FakeSecureStorage();
            Save(DatabaseKeyStore.CreateProtection(storage), 7);
            storage.FailedReads = 2;
            storage.IsWriteRejected = true;
            LogAssert.Expect(LogType.Warning, new Regex("could not be read"));
            LogAssert.Expect(LogType.Error, new Regex("could not be stored"));

            var protection = DatabaseKeyStore.CreateProtection(storage);

            Assert.That(Load(protection), Is.EqualTo(7));
        }

        private void Save(DatabaseProtection protection, int id)
        {
            using var database = new FileDatabase(_directory, protection);
            database.GetDocument<KeyStoreRecord>("profile").Set(new KeyStoreRecord { Id = id });
        }

        private int Load(DatabaseProtection protection)
        {
            using var database = new FileDatabase(_directory, protection);
            return database.GetDocument<KeyStoreRecord>("profile").Value.Id;
        }

        private sealed class FakeSecureStorage : ISecureStorage
        {
            private readonly Dictionary<string, string> _values = new();

            public int FailedReads { get; set; }
            public bool IsWriteRejected { get; set; }
            public int WriteCount { get; private set; }

            public SecureStorageReadResult Read(string key, out string? value)
            {
                if (FailedReads > 0)
                {
                    FailedReads--;
                    value = null;
                    return SecureStorageReadResult.Failed;
                }

                return _values.TryGetValue(key, out value) ? SecureStorageReadResult.Found : SecureStorageReadResult.Missing;
            }

            public bool Write(string key, string value)
            {
                if (IsWriteRejected)
                {
                    return false;
                }

                WriteCount++;
                _values[key] = value;
                return true;
            }

            public bool Delete(string key)
            {
                return _values.Remove(key);
            }
        }
    }

    [GroveGames.Serialization.Schema]
    public sealed class KeyStoreRecord
    {
        public int Id;
    }
}
