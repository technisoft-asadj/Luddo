using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ludo.EditorTools
{
    /// <summary>
    /// A Windows "test client" build, only for testing online matches on one computer (the Editor hosts, this build
    /// joins). It is a Development build, so it contains the test helpers; it is never shipped. The project is switched
    /// back to Android when the build is done.
    /// </summary>
    public static class DevBuilds
    {
        public const string WindowsOutput = "Builds/WinTest/LudoFight.exe";

        [MenuItem("Ludo/Build Windows Test Client")]
        public static void BuildWindowsTestClient()
        {
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();      // otherwise Unity asks "save the scenes?" and waits for a click
            string[] scenes = { "Assets/_Game/Scenes/Menu.unity", "Assets/_Game/Scenes/Game.unity" };
            Directory.CreateDirectory(Path.GetDirectoryName(WindowsOutput));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WindowsOutput,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Ludo] Windows test client: " + report.summary.result + " (" + report.summary.totalErrors + " errors) -> " + WindowsOutput);
            File.WriteAllText("Builds/WinTest/result.txt", report.summary.result.ToString());
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }
}
