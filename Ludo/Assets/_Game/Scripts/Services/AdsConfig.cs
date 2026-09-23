using UnityEngine;

namespace Ludo.Services
{
    /// <summary>
    /// Everything about ads that you may want to change without touching code: the ad unit IDs, whether to use
    /// Google's TEST ads, and how often an interstitial may appear. Lives in Resources/AdsConfig.asset.
    /// While useTestAds is on (the default) the game only ever asks Google for its official test ads, which is the
    /// only safe way to develop: clicking your own real ads can get an AdMob account suspended.
    /// </summary>
    [CreateAssetMenu(fileName = "AdsConfig", menuName = "Ludo/Ads Config")]
    public sealed class AdsConfig : ScriptableObject
    {
        // Google's public sample IDs (documented at developers.google.com/admob/unity). They always return test ads.
        public const string TestAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string TestAndroidInterstitialId = "ca-app-pub-3940256099942544/1033173712";
        public const string TestAndroidBannerId = "ca-app-pub-3940256099942544/6300978111";
        public const string TestAndroidRewardedId = "ca-app-pub-3940256099942544/5224354917";

        [Header("Master switch")]
        public bool adsEnabled = true;
        [Tooltip("ON = Google test ads only. Turn OFF only for the final Play Store build, after entering your real IDs.")]
        public bool useTestAds = true;

        [Header("Your real AdMob IDs (used only when Use Test Ads is off)")]
        [Tooltip("Looks like ca-app-pub-1234567890123456~1234567890 (note the ~)")]
        public string androidAppId = "";
        [Tooltip("Looks like ca-app-pub-1234567890123456/1234567890 (note the /)")]
        public string androidInterstitialId = "";
        [Tooltip("Shown only at the bottom of the main menu, never during a game.")]
        public string androidBannerId = "";
        [Tooltip("The optional \"double your Rank Points\" ad after an online win. Create a Rewarded ad unit in AdMob and paste its ID here.")]
        public string androidRewardedId = "";

        [Header("Banner")]
        [Tooltip("A small banner at the bottom of the MAIN MENU only. Never shown during a game.")]
        public bool showMenuBanner = true;

        [Header("How often an interstitial may appear (only between games, never during one)")]
        [Min(0)] public int firstAdAfterGames = 2;          // the first games of a session are ad-free
        [Min(0)] public int minGamesBetweenAds = 2;
        [Min(0)] public float minSecondsBetweenAds = 120f;

        public string AppId => useTestAds || string.IsNullOrWhiteSpace(androidAppId) ? TestAndroidAppId : androidAppId.Trim();

        public string InterstitialId =>
            useTestAds || string.IsNullOrWhiteSpace(androidInterstitialId) ? TestAndroidInterstitialId : androidInterstitialId.Trim();

        public string BannerId =>
            useTestAds || string.IsNullOrWhiteSpace(androidBannerId) ? TestAndroidBannerId : androidBannerId.Trim();

        public string RewardedId =>
            useTestAds || string.IsNullOrWhiteSpace(androidRewardedId) ? TestAndroidRewardedId : androidRewardedId.Trim();

        /// <summary>An AdMob app ID has a "~", an ad unit ID has a "/" - mixing them up is the most common setup mistake.</summary>
        public static bool LooksLikeAppId(string id) => !string.IsNullOrWhiteSpace(id) && id.StartsWith("ca-app-pub-") && id.Contains("~") && !id.Contains("/");

        public static bool LooksLikeAdUnitId(string id) => !string.IsNullOrWhiteSpace(id) && id.StartsWith("ca-app-pub-") && id.Contains("/") && !id.Contains("~");
    }
}
