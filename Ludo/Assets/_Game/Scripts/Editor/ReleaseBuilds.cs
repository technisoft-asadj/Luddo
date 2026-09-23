using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ludo.EditorTools
{
    /// <summary>
    /// The two release builds. Each one sets everything a release needs, builds, and then puts the project settings back
    /// exactly as they were (so day-to-day development builds stay safe: test ads, trace logs, debug signing).
    ///   Ludo > Release > Build Play Store AAB      real ads, signed, no trace logs  -> upload this to Google Play
    ///   Ludo > Release > Build Test-Ads APK        Google TEST ads, signed          -> install on a phone to try the release build
    /// The signing password is read from ..\..\Keystore\keystore-info.txt (outside the project and outside git).
    /// The result is written to Builds/Release/result-*.txt (a build blocks the Editor for many minutes).
    ///
    /// Two steps, because removing the LUDO_TRACE define makes Unity recompile (which would abort a running build):
    /// step 1 changes the settings and waits for the recompile, step 2 (Resume) builds and restores the settings.
    /// </summary>
    public static class ReleaseBuilds
    {
        const string Version = "1.0.0";
        const int VersionCode = 1;
        const string OutputFolder = "Builds/Release";
        const string KeystoreInfo = "../../Keystore/keystore-info.txt";
        const string AdsConfigPath = "Assets/_Game/Resources/AdsConfig.asset";
        const string BackupPath = "Library/ludo-release-backup.json";

        [Serializable]
        class Backup
        {
            public string phase = "prepared";          // "prepared" -> "building"
            public string[] defines;
            public bool bundle, development, customKeystore, testAds;
            public string keystore, keystorePass, alias, aliasPass, version;
            public int versionCode;
            // the request
            public bool wantBundle, wantTestAds;
            public string file, result;
        }

        [MenuItem("Ludo/Release/Build Play Store AAB")]
        public static void BuildAab() => Begin(bundle: true, testAds: false, file: "LudoFight-" + Version + ".aab", result: "result-aab.txt");

        [MenuItem("Ludo/Release/Build Test-Ads APK")]
        public static void BuildTestApk() => Begin(bundle: false, testAds: true, file: "LudoFight-" + Version + "-testads.apk", result: "result-apk.txt");

        // ---------- step 1: change the settings ----------

        static void Begin(bool bundle, bool testAds, string file, string result)
        {
            if (File.Exists(BackupPath)) Restore();                      // a broken earlier run: go back to normal first
            Directory.CreateDirectory(OutputFolder);
            string resultPath = Path.Combine(OutputFolder, result);
            if (File.Exists(resultPath)) File.Delete(resultPath);
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            var target = NamedBuildTarget.Android;
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] oldDefines);
            var ads = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(AdsConfigPath));
            var backup = new Backup
            {
                defines = oldDefines,
                bundle = EditorUserBuildSettings.buildAppBundle,
                development = EditorUserBuildSettings.development,
                customKeystore = PlayerSettings.Android.useCustomKeystore,
                keystore = PlayerSettings.Android.keystoreName,
                keystorePass = PlayerSettings.Android.keystorePass,
                alias = PlayerSettings.Android.keyaliasName,
                aliasPass = PlayerSettings.Android.keyaliasPass,
                version = PlayerSettings.bundleVersion,
                versionCode = PlayerSettings.Android.bundleVersionCode,
                testAds = ads.FindProperty("useTestAds").boolValue,
                wantBundle = bundle, wantTestAds = testAds, file = file, result = result
            };
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup));

            try
            {
                var keystore = ReadKeystoreInfo();
                EditorUserBuildSettings.buildAppBundle = bundle;
                EditorUserBuildSettings.development = false;
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore.path;
                PlayerSettings.Android.keystorePass = keystore.storePass;
                PlayerSettings.Android.keyaliasName = keystore.alias;
                PlayerSettings.Android.keyaliasPass = keystore.keyPass;
                PlayerSettings.bundleVersion = Version;
                PlayerSettings.Android.bundleVersionCode = VersionCode;
                ads.FindProperty("useTestAds").boolValue = testAds;
                ads.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                string[] wanted = oldDefines.Where(d => d != "LUDO_TRACE").ToArray();
                if (wanted.Length != oldDefines.Length)
                    PlayerSettings.SetScriptingDefineSymbols(target, wanted);   // Unity recompiles; Resume() runs when it is done
                else
                    ResumeWhenIdle();                                           // nothing to recompile
            }
            catch (Exception e)
            {
                File.WriteAllText(resultPath, "Failed: " + e.Message);
                Debug.LogError("[Ludo] Release build could not start: " + e);
                Restore();
            }
        }

        // ---------- step 2: build, then put everything back ----------

        [InitializeOnLoadMethod]
        static void OnLoad()
        {
            if (File.Exists(BackupPath)) ResumeWhenIdle();
        }

        static double readyAt;

        /// <summary>Wait until Unity has finished compiling and importing (plus a few quiet seconds), then run step 2.</summary>
        static void ResumeWhenIdle()
        {
            readyAt = 0;
            EditorApplication.update -= WaitThenResume;
            EditorApplication.update += WaitThenResume;
        }

        static void WaitThenResume()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { readyAt = 0; return; }
            if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 4; return; }
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= WaitThenResume;
            Resume();
        }

        static void Resume()
        {
            if (!File.Exists(BackupPath)) return;
            var backup = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
            string resultPath = Path.Combine(OutputFolder, backup.result);
            if (backup.phase == "building")
            {
                // the Editor was restarted in the middle of a build: give up and restore the normal settings
                File.WriteAllText(resultPath, "Interrupted");
                Restore();
                return;
            }
            backup.phase = "building";
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup));

            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/Menu.unity", "Assets/_Game/Scenes/Game.unity" },
                    locationPathName = Path.Combine(OutputFolder, backup.file),
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                string text = report.summary.result + " | errors " + report.summary.totalErrors + " | " +
                              (report.summary.totalSize / (1024 * 1024)) + " MB | " + options.locationPathName;
                File.WriteAllText(resultPath, text);
                Debug.Log("[Ludo] Release build: " + text);
            }
            catch (Exception e)
            {
                File.WriteAllText(resultPath, "Failed: " + e.Message);
                Debug.LogError("[Ludo] Release build failed: " + e);
            }
            finally
            {
                Restore();
            }
        }

        /// <summary>Put the project settings back exactly as they were before the release build.</summary>
        static void Restore()
        {
            if (!File.Exists(BackupPath)) return;
            var b = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
            File.Delete(BackupPath);
            EditorUserBuildSettings.buildAppBundle = b.bundle;
            EditorUserBuildSettings.development = b.development;
            PlayerSettings.Android.useCustomKeystore = b.customKeystore;
            PlayerSettings.Android.keystoreName = b.keystore;
            PlayerSettings.Android.keystorePass = b.keystorePass;
            PlayerSettings.Android.keyaliasName = b.alias;
            PlayerSettings.Android.keyaliasPass = b.aliasPass;
            PlayerSettings.bundleVersion = b.version;
            PlayerSettings.Android.bundleVersionCode = b.versionCode;
            var ads = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(AdsConfigPath));
            ads.FindProperty("useTestAds").boolValue = b.testAds;
            ads.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, b.defines);
        }

        static (string path, string alias, string storePass, string keyPass) ReadKeystoreInfo()
        {
            string info = Path.GetFullPath(KeystoreInfo);
            if (!File.Exists(info)) throw new FileNotFoundException("Keystore info not found: " + info);
            string path = null, alias = null, store = null, key = null;
            foreach (var line in File.ReadAllLines(info))
            {
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string name = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
                if (name == "Keystore file") path = value;
                else if (name == "Alias") alias = value;
                else if (name == "Store password") store = value;
                else if (name == "Key password") key = value;
            }
            if (path == null || alias == null || store == null || key == null) throw new InvalidDataException("keystore-info.txt is incomplete");
            return (path, alias, store, key);
        }
    }
}
