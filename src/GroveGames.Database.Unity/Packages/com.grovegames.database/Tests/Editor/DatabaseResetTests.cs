using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace GroveGames.Database.Unity.Editor.Tests
{
    public sealed class DatabaseResetTests
    {
        private string _directory = null!;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "GroveGames.Database.Unity.Tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "profile.db"), "profile");
            File.WriteAllText(Path.Combine(_directory, "items.db"), "items");
        }

        [TearDown]
        public void TearDown()
        {
            DatabaseReset.Unschedule(_directory, DatabaseReset.AllStores);
            DatabaseReset.Unschedule(_directory, "profile");

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void Apply_ScheduledStore_DeletesOnlyThatStore()
        {
            DatabaseReset.Schedule(_directory, "profile");

            DatabaseReset.Apply(_directory);

            Assert.That(File.Exists(Path.Combine(_directory, "profile.db")), Is.False);
            Assert.That(File.Exists(Path.Combine(_directory, "items.db")), Is.True);
        }

        [Test]
        public void Apply_AllStoresScheduled_DeletesTheDirectory()
        {
            DatabaseReset.Schedule(_directory, DatabaseReset.AllStores);

            DatabaseReset.Apply(_directory);

            Assert.That(Directory.Exists(_directory), Is.False);
        }

        [Test]
        public void Apply_AfterApplying_ClearsTheSchedule()
        {
            var names = new List<string>();
            DatabaseReset.Schedule(_directory, "profile");

            DatabaseReset.Apply(_directory);
            DatabaseReset.GetScheduled(_directory, names);

            Assert.That(names, Is.Empty);
        }

        [Test]
        public void Unschedule_ScheduledStore_KeepsTheStore()
        {
            DatabaseReset.Schedule(_directory, "profile");

            DatabaseReset.Unschedule(_directory, "profile");
            DatabaseReset.Apply(_directory);

            Assert.That(File.Exists(Path.Combine(_directory, "profile.db")), Is.True);
        }

        [Test]
        public void Schedule_SameStoreTwice_ListsItOnce()
        {
            var names = new List<string>();

            DatabaseReset.Schedule(_directory, "profile");
            DatabaseReset.Schedule(_directory, "profile");
            DatabaseReset.GetScheduled(_directory, names);

            Assert.That(names, Is.EqualTo(new[] { "profile" }));
        }
    }
}
