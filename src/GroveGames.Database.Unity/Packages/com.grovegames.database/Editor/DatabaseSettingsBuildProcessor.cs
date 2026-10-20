using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GroveGames.Database.Unity.Editor
{
    internal sealed class DatabaseSettingsBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            DatabaseSettingsAsset.GetOrCreate();
        }
    }
}
