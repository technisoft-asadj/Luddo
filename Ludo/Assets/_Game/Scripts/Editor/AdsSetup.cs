using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Ludo.Services;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Creates Resources/AdsConfig.asset and keeps the Google plugin's Android App ID equal to the one in that config
    /// (Google's TEST app ID while "Use Test Ads" is on). The plugin refuses to build without an app ID, and a wrong
    /// one makes the app crash at start, so the same sync also runs automatically before every Android build.
    /// </summary>
    public static class AdsSetup
    {
        const string ConfigFolder = "Assets/_Game/Resources";
        const string ConfigPath = ConfigFolder + "/AdsConfig.asset";

        [MenuItem("Ludo/Setup Ads")]
        public static void Run()
        {
            var config = LoadOrCreateConfig();
            SyncAppId(config);
            Debug.Log("[Ludo] Ads ready. Test ads: " + config.useTestAds + ", app ID: " + config.AppId);
        }

        public static AdsConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<AdsConfig>(ConfigPath);
            if (config != null) return config;
            Directory.CreateDirectory(ConfigFolder);
            config = ScriptableObject.CreateInstance<AdsConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>Copies the app ID into the plugin's own settings asset (created by the plugin's loader if missing).</summary>
        public static void SyncAppId(AdsConfig config)
        {
            // The plugin keeps its settings class internal, so it is reached by name (the same way its own menu does).
            // LoadInstance() is the plugin's own "find or create the settings asset" call.
            var type = System.Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            var load = type != null ? type.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
            var settings = load != null ? load.Invoke(null, null) as ScriptableObject : null;
            var property = type != null ? type.GetProperty("GoogleMobileAdsAndroidAppId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
            if (settings == null || property == null)
            {
                Debug.LogError("[Ludo] Could not reach the Google Mobile Ads settings (plugin version changed?). Set the App ID in Assets > Google Mobile Ads > Settings.");
                return;
            }

            if ((string)property.GetValue(settings) != config.AppId)
            {
                property.SetValue(settings, config.AppId);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }
    }

    /// <summary>Runs before every Android build: syncs the app ID and refuses to build a real-ads release with bad IDs.</summary>
    public sealed class AdsBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;   // before the Google plugin's own build steps

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var config = AssetDatabase.LoadAssetAtPath<AdsConfig>("Assets/_Game/Resources/AdsConfig.asset");
            if (config == null) config = AdsSetup.LoadOrCreateConfig();
            if (!config.adsEnabled) { Debug.Log("[Ludo] Ads are disabled in AdsConfig."); return; }

            if (!config.useTestAds)
            {
                if (!AdsConfig.LooksLikeAppId(config.androidAppId))
                    throw new BuildFailedException("AdsConfig: Android App ID is empty or wrong. It must look like ca-app-pub-1234567890123456~1234567890 (with a ~).");
                if (!AdsConfig.LooksLikeAdUnitId(config.androidInterstitialId))
                    throw new BuildFailedException("AdsConfig: Interstitial ad unit ID is empty or wrong. It must look like ca-app-pub-1234567890123456/1234567890 (with a /).");
                // banner / rewarded are not used yet, but a typo there would only surface later, so check them too when filled in
                if (!string.IsNullOrWhiteSpace(config.androidBannerId) && !AdsConfig.LooksLikeAdUnitId(config.androidBannerId))
                    throw new BuildFailedException("AdsConfig: Banner ad unit ID looks wrong. It must look like ca-app-pub-1234567890123456/1234567890 (with a /).");
                if (!string.IsNullOrWhiteSpace(config.androidRewardedId) && !AdsConfig.LooksLikeAdUnitId(config.androidRewardedId))
                    throw new BuildFailedException("AdsConfig: Rewarded ad unit ID looks wrong. It must look like ca-app-pub-1234567890123456/1234567890 (with a /).");
                Debug.Log("[Ludo] Building with REAL ads. Never tap your own ads while testing.");
            }
            else
            {
                Debug.LogWarning("[Ludo] Building with Google TEST ads (AdsConfig > Use Test Ads). Turn it off only for the final Play Store build.");
            }
            AdsSetup.SyncAppId(config);
        }
    }
}
