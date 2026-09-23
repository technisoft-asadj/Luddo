using NUnit.Framework;
using UnityEngine;
using Ludo.Services;

namespace Ludo.Tests
{
    /// <summary>The rules for when an interstitial may appear, and the ID safety helpers. No SDK is involved.</summary>
    public class AdPolicyTests
    {
        AdsConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<AdsConfig>();
            config.firstAdAfterGames = 2;
            config.minGamesBetweenAds = 2;
            config.minSecondsBetweenAds = 120f;
        }

        [TearDown]
        public void Destroy() => Object.DestroyImmediate(config);

        [Test]
        public void NoAd_BeforeTheFirstFreeGamesAreFinished()
        {
            Assert.IsFalse(AdPolicy.ShouldShow(config, 0, 0, 999f));
            Assert.IsFalse(AdPolicy.ShouldShow(config, 1, 1, 999f));
        }

        [Test]
        public void Ad_AfterEnoughGamesAndTime()
        {
            Assert.IsTrue(AdPolicy.ShouldShow(config, 2, 2, 120f));
        }

        [Test]
        public void NoAd_IfTooFewGamesSinceTheLastAd()
        {
            Assert.IsFalse(AdPolicy.ShouldShow(config, 10, 1, 999f));
        }

        [Test]
        public void NoAd_IfTooLittleTimeSinceTheLastAd()
        {
            Assert.IsFalse(AdPolicy.ShouldShow(config, 10, 5, 60f));
        }

        [Test]
        public void NoAd_WhenAdsAreSwitchedOffOrMissing()
        {
            config.adsEnabled = false;
            Assert.IsFalse(AdPolicy.ShouldShow(config, 10, 10, 999f));
            Assert.IsFalse(AdPolicy.ShouldShow(null, 10, 10, 999f));
        }

        [Test]
        public void TestAds_AreUsedByDefaultAndWheneverRealIdsAreMissing()
        {
            config.useTestAds = true;
            config.androidAppId = "ca-app-pub-1234567890123456~1234567890";
            config.androidInterstitialId = "ca-app-pub-1234567890123456/1234567890";
            Assert.AreEqual(AdsConfig.TestAndroidInterstitialId, config.InterstitialId);   // test mode wins over real IDs
            Assert.AreEqual(AdsConfig.TestAndroidAppId, config.AppId);

            config.useTestAds = false;
            Assert.AreEqual("ca-app-pub-1234567890123456/1234567890", config.InterstitialId);
            Assert.AreEqual("ca-app-pub-1234567890123456~1234567890", config.AppId);

            config.androidInterstitialId = "  ";                                          // real mode but nothing entered: never send an empty ID
            Assert.AreEqual(AdsConfig.TestAndroidInterstitialId, config.InterstitialId);
        }

        [Test]
        public void BannerAndRewarded_FollowTheSameTestVersusRealRule()
        {
            config.androidBannerId = "ca-app-pub-1234567890123456/1111111111";
            config.androidRewardedId = "ca-app-pub-1234567890123456/2222222222";
            config.useTestAds = true;
            Assert.AreEqual(AdsConfig.TestAndroidBannerId, config.BannerId);
            Assert.AreEqual(AdsConfig.TestAndroidRewardedId, config.RewardedId);
            config.useTestAds = false;
            Assert.AreEqual("ca-app-pub-1234567890123456/1111111111", config.BannerId);
            Assert.AreEqual("ca-app-pub-1234567890123456/2222222222", config.RewardedId);
        }

        [Test]
        public void ShippedConfig_IdsAreWellFormedAndBelongToOnePublisher()
        {
            var shipped = Resources.Load<AdsConfig>("AdsConfig");
            Assert.IsNotNull(shipped, "Resources/AdsConfig.asset is missing - run Ludo > Setup Ads");
            if (string.IsNullOrWhiteSpace(shipped.androidAppId)) Assert.Pass("no real IDs entered yet");

            Assert.IsTrue(AdsConfig.LooksLikeAppId(shipped.androidAppId), "App ID must contain ~");
            Assert.IsTrue(AdsConfig.LooksLikeAdUnitId(shipped.androidInterstitialId), "Interstitial ID must contain /");
            Assert.IsTrue(AdsConfig.LooksLikeAdUnitId(shipped.androidBannerId), "Banner ID must contain /");
            Assert.IsTrue(AdsConfig.LooksLikeAdUnitId(shipped.androidRewardedId), "Rewarded ID must contain /");

            // all four IDs of one app share the publisher number "ca-app-pub-<publisher>"; a different one means a copy/paste mistake
            string publisher = shipped.androidAppId.Substring(0, shipped.androidAppId.IndexOf('~'));
            foreach (var unit in new[] { shipped.androidInterstitialId, shipped.androidBannerId, shipped.androidRewardedId })
                StringAssert.StartsWith(publisher + "/", unit);
        }

        [Test]
        public void IdFormat_CatchesTheCommonMixUp()
        {
            Assert.IsTrue(AdsConfig.LooksLikeAppId("ca-app-pub-1234567890123456~1234567890"));
            Assert.IsFalse(AdsConfig.LooksLikeAppId("ca-app-pub-1234567890123456/1234567890"));   // an ad unit ID pasted as an app ID
            Assert.IsTrue(AdsConfig.LooksLikeAdUnitId("ca-app-pub-1234567890123456/1234567890"));
            Assert.IsFalse(AdsConfig.LooksLikeAdUnitId("ca-app-pub-1234567890123456~1234567890"));
            Assert.IsFalse(AdsConfig.LooksLikeAppId(""));
            Assert.IsFalse(AdsConfig.LooksLikeAdUnitId(null));
        }

        [Test]
        public void DefaultConfig_UsesGoogleTestIdsOnly()
        {
            Assert.IsTrue(config.useTestAds);
            Assert.IsTrue(config.showMenuBanner);
            Assert.IsTrue(config.BannerId.StartsWith("ca-app-pub-3940256099942544"));         // banner is a test ad too in test mode
            Assert.IsTrue(config.InterstitialId.StartsWith("ca-app-pub-3940256099942544"));   // Google's public sample publisher
            Assert.IsTrue(config.AppId.StartsWith("ca-app-pub-3940256099942544"));
        }
    }
}
