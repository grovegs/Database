using NUnit.Framework;
using UnityEditor;

namespace GroveGames.Database.Unity.Editor.Tests
{
    public sealed class DatabaseSettingsAssetTests
    {
        [Test]
        public void GetOrCreate_SettingsAsset_IsReturnedAtRuntime()
        {
            var asset = DatabaseSettingsAsset.GetOrCreate();

            var settings = DatabaseSettings.GetOrCreate();

            Assert.AreSame(asset, settings);
        }

        [Test]
        public void GetOrCreate_SettingsAsset_IsPreloaded()
        {
            var asset = DatabaseSettingsAsset.GetOrCreate();

            var preloaded = PlayerSettings.GetPreloadedAssets();

            Assert.Contains(asset, preloaded);
        }
    }
}
