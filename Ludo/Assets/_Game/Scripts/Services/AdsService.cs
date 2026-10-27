using System;
using System.Collections;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Ludo.Services
{
    /// <summary>
    /// The only class that talks to the Google Mobile Ads SDK. The game asks for two things: "a game just finished"
    /// (NotifyGameFinished) and "show an ad now if it is allowed, then continue" (ShowInterstitialIfDue). Everything
    /// else - the privacy consent form, starting the SDK, loading the next ad, retrying after a failure, deciding
    /// whether an ad is due - happens in here. If ads are switched off, missing or fail, the game simply continues.
    ///
    /// Order at launch (Google's rule): ask for consent FIRST (UMP form, only shown where the law requires it), and only
    /// start the ads SDK once ConsentInformation.CanRequestAds() is true.
    /// </summary>
    public sealed class AdsService : MonoBehaviour
    {
        const float AdShowWatchdogSeconds = 120f;   // if Google never tells us the ad closed, let the player continue anyway

        static AdsService instance;

        AdsConfig config;
        bool sdkStarted;
        bool loadingAd;
        InterstitialAd interstitial;
        int loadFailures;

        int gamesFinished;
        int gamesSinceLastAd;
        float lastAdTime;                      // realtimeSinceStartup when the last interstitial was shown (or app start)

        RewardedAd rewarded;                   // the optional "watch an ad for a bonus" ad
        bool loadingRewarded;
        int rewardedFailures;
        Action<bool> pendingRewarded;          // the result screen waiting for the rewarded ad to end (true = the player earned the reward)
        bool rewardEarned;

        Action pendingContinue;                // what to do once the ad closes
        Coroutine watchdog;

        BannerView banner;                     // the main-menu banner (created on first use, then only hidden/shown)
        bool bannerWanted;                     // is the main menu on screen right now?
        bool bannerLoaded;
        int bannerFailures;

        AppOpenAd appOpen;                     // shown when the player comes back to the app (main menu only)
        bool loadingAppOpen;
        int appOpenFailures;
        DateTime appOpenExpires;               // Google: a loaded App Open ad is good for 4 hours
        bool appOpenShowing;
        DateTime? leftAt;                      // when the app went to the background
        float lastAppOpenShown = -9999f;
        static int appOpenBlockers;            // rooms and games raise this so no ad appears over them

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; appOpenBlockers = 0; }

        /// <summary>A screen that must never be covered by an App Open ad (the waiting room) calls this with true when it opens and false when it closes.</summary>
        public static void BlockAppOpen(bool blocked) => appOpenBlockers = Mathf.Max(0, appOpenBlockers + (blocked ? 1 : -1));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // the ads plugin exists for Android (and the Editor's placeholder ads); a Windows test build must not touch it
            if (Application.platform != RuntimePlatform.Android && !Application.isEditor) return;
            var cfg = Resources.Load<AdsConfig>("AdsConfig");
            if (cfg == null || !cfg.adsEnabled)
            {
                Debug.Log("[Ludo] Ads are off (no AdsConfig or adsEnabled = false).");
                return;
            }
            var go = new GameObject("AdsService");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AdsService>();
            instance.config = cfg;
            instance.lastAdTime = Time.realtimeSinceStartup;     // no ad in the first minutes of a session
        }

        // ---------- what the game calls ----------

        /// <summary>Tell the service a game reached its end (a winner was decided).</summary>
        public static void NotifyGameFinished()
        {
            if (instance == null) return;
            instance.gamesFinished++;
            instance.gamesSinceLastAd++;
        }

        /// <summary>
        /// Show an interstitial if the policy allows it and one is loaded, then call 'onContinue' once the player is back
        /// (or immediately, if no ad is shown). 'onContinue' is called exactly once.
        /// </summary>
        public static void ShowInterstitialIfDue(Action onContinue)
        {
            if (instance == null) { onContinue?.Invoke(); return; }
            instance.TryShow(onContinue);
        }

        /// <summary>Is a rewarded ad loaded and ready to show right now?</summary>
        public static bool RewardedReady => instance != null && instance.sdkStarted && instance.rewarded != null && instance.rewarded.CanShowAd();

        /// <summary>
        /// Show the optional rewarded ad. 'onDone(true)' is called ONLY when the ads SDK confirmed that the player earned the reward
        /// (watched to the end); it is called at most once. 'onDone(false)' when the ad is missing, fails, or is closed early. The
        /// caller must never grant anything on false, and must never make the ad mandatory.
        /// </summary>
        public static void ShowRewarded(Action<bool> onDone)
        {
            if (instance == null || !RewardedReady || instance.pendingRewarded != null) { onDone?.Invoke(false); return; }
            instance.ShowRewardedAd(onDone);
        }

        /// <summary>Show the banner (main menu is visible) or hide it (any other screen, or the game). Safe to call at any time.</summary>
        public static void SetMenuBannerVisible(bool visible)
        {
            if (instance == null) return;
            instance.bannerWanted = visible;
            instance.ApplyBanner();
        }

        /// <summary>True in regions where the law requires a "Privacy settings" entry in the app (GDPR etc.).</summary>
        public static bool PrivacyOptionsRequired =>
            instance != null && instance.sdkStarted &&
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        /// <summary>Open the consent form again so the player can change their privacy choices.</summary>
        public static void ShowPrivacyOptions(Action onDone)
        {
            if (instance == null) { onDone?.Invoke(); return; }
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null) Debug.LogWarning("[Ludo] Privacy options form: " + error.Message);
                onDone?.Invoke();
            });
        }

        // ---------- start-up: consent, then the SDK ----------

        IEnumerator Start()
        {
            yield return null;   // one frame, so the first screen is already visible behind the consent form

            // A returning player who already gave (or did not need to give) consent may start the SDK straight away.
            if (ConsentInformation.CanRequestAds()) StartSdk();

            var parameters = new ConsentRequestParameters();   // audience is 13+, so no "under age of consent" flag
            ConsentInformation.Update(parameters, OnConsentInfoUpdated);
        }

        void OnConsentInfoUpdated(FormError error)
        {
            if (error != null)
            {
                Debug.LogWarning("[Ludo] Consent info update failed: " + error.Message);
                return;
            }
            // shows the consent form only if this player's region requires it
            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null) Debug.LogWarning("[Ludo] Consent form: " + formError.Message);
                if (ConsentInformation.CanRequestAds()) StartSdk();
            });
        }

        void StartSdk()
        {
            if (sdkStarted) return;
            sdkStarted = true;

            // family-friendly content only; ads are still personalised or not according to the player's consent choice
            MobileAds.SetRequestConfiguration(new RequestConfiguration { MaxAdContentRating = MaxAdContentRating.PG });
            MobileAds.Initialize(status =>
            {
                Debug.Log("[Ludo] Ads SDK ready (test ads: " + config.useTestAds + ")");
                LoadInterstitial();
                LoadRewarded();
                LoadAppOpen();
                ApplyBanner();      // the main menu may already be waiting for its banner
            });
        }

        // ---------- App Open ad (when the player returns to the app) ----------

        void OnApplicationPause(bool paused)
        {
            if (paused) { leftAt = DateTime.UtcNow; return; }
            if (leftAt == null) return;
            float away = (float)(DateTime.UtcNow - leftAt.Value).TotalSeconds;
            leftAt = null;
            TryShowAppOpen(away);
        }

        void TryShowAppOpen(float secondsAway)
        {
            if (!sdkStarted) return;
            bool busy = pendingContinue != null || pendingRewarded != null || appOpenShowing;
            // only on the main menu: never over a match, and never over the waiting room
            bool blocked = busy || appOpenBlockers > 0 || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Menu";
            bool allowed = AdPolicy.ShouldShowAppOpen(config, secondsAway, Time.realtimeSinceStartup - lastAppOpenShown,
                Time.realtimeSinceStartup - lastAdTime, blocked);
            if (!allowed) return;
            if (appOpen == null || !appOpen.CanShowAd() || DateTime.UtcNow >= appOpenExpires)
            {
                if (appOpen != null) { appOpen.Destroy(); appOpen = null; }
                LoadAppOpen();          // nothing good to show now; have one ready for the next return
                return;
            }
            appOpenShowing = true;
            lastAppOpenShown = Time.realtimeSinceStartup;
            appOpen.Show();
        }

        void LoadAppOpen()
        {
            if (!config.showAppOpenAd || loadingAppOpen || appOpen != null) return;
            loadingAppOpen = true;
            AppOpenAd.Load(config.AppOpenId, new AdRequest(), (ad, error) =>
            {
                loadingAppOpen = false;
                if (error != null || ad == null)
                {
                    appOpenFailures++;
                    Debug.LogWarning("[Ludo] App Open ad failed to load: " + (error != null ? error.GetMessage() : "no ad"));
                    StartCoroutine(RetryAppOpenLater());
                    return;
                }
                appOpenFailures = 0;
                appOpen = ad;
                appOpenExpires = DateTime.UtcNow.AddHours(3.5);       // a little under Google's 4 hours
                Debug.Log("[Ludo] App Open ad loaded and ready.");
                ad.OnAdFullScreenContentClosed += EndAppOpen;
                ad.OnAdFullScreenContentFailed += adError =>
                {
                    Debug.LogWarning("[Ludo] App Open ad failed to show: " + adError.GetMessage());
                    EndAppOpen();
                };
            });
        }

        IEnumerator RetryAppOpenLater()
        {
            float wait = Mathf.Min(15f * Mathf.Pow(2f, Mathf.Min(appOpenFailures - 1, 3)), 120f);
            yield return new WaitForSecondsRealtime(wait);
            LoadAppOpen();
        }

        void EndAppOpen()
        {
            if (appOpen != null) { appOpen.Destroy(); appOpen = null; }        // an ad can be shown only once
            appOpenShowing = false;
            lastAdTime = Time.realtimeSinceStartup;                            // no interstitial right behind it
            LoadAppOpen();
        }

        // ---------- banner (main menu only) ----------

        void ApplyBanner()
        {
            if (!sdkStarted || !config.showMenuBanner) return;

            if (!bannerWanted)
            {
                if (banner != null) banner.Hide();
                return;
            }
            if (banner == null) CreateBanner();
            else if (bannerLoaded) banner.Show();
        }

        void CreateBanner()
        {
            // adaptive banner: as wide as the screen, the height Google picks for that width
            var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            banner = new BannerView(config.BannerId, size, AdPosition.Bottom);
            banner.OnBannerAdLoaded += () =>
            {
                bannerLoaded = true;
                bannerFailures = 0;
                Debug.Log("[Ludo] Banner loaded (" + banner.GetHeightInPixels() + " px high).");
                ApplyBanner();      // shows it only if the main menu is still on screen
            };
            banner.OnBannerAdLoadFailed += error =>
            {
                bannerFailures++;
                Debug.LogWarning("[Ludo] Banner failed to load: " + error.GetMessage());
                StartCoroutine(RetryBannerLater());
            };
            banner.Hide();          // stays hidden until the menu asks for it and it has loaded
            banner.LoadAd(new AdRequest());
        }

        IEnumerator RetryBannerLater()
        {
            yield return new WaitForSecondsRealtime(Mathf.Min(30f * bannerFailures, 120f));
            if (banner != null && !bannerLoaded && bannerWanted) banner.LoadAd(new AdRequest());
        }

        // ---------- loading ----------

        void LoadInterstitial()
        {
            if (loadingAd || interstitial != null) return;
            loadingAd = true;
            InterstitialAd.Load(config.InterstitialId, new AdRequest(), (ad, error) =>
            {
                loadingAd = false;
                if (error != null || ad == null)
                {
                    loadFailures++;
                    Debug.LogWarning("[Ludo] Interstitial failed to load: " + (error != null ? error.GetMessage() : "no ad"));
                    StartCoroutine(RetryLoadLater());
                    return;
                }
                loadFailures = 0;
                interstitial = ad;
                Debug.Log("[Ludo] Interstitial loaded and ready.");
                ad.OnAdFullScreenContentClosed += OnAdClosed;
                ad.OnAdFullScreenContentFailed += adError =>
                {
                    Debug.LogWarning("[Ludo] Interstitial failed to show: " + adError.GetMessage());
                    OnAdClosed();
                };
            });
        }

        void LoadRewarded()
        {
            if (loadingRewarded || rewarded != null) return;
            loadingRewarded = true;
            RewardedAd.Load(config.RewardedId, new AdRequest(), (ad, error) =>
            {
                loadingRewarded = false;
                if (error != null || ad == null)
                {
                    rewardedFailures++;
                    Debug.LogWarning("[Ludo] Rewarded ad failed to load: " + (error != null ? error.GetMessage() : "no ad"));
                    StartCoroutine(RetryRewardedLater());
                    return;
                }
                rewardedFailures = 0;
                rewarded = ad;
                Debug.Log("[Ludo] Rewarded ad loaded and ready.");
                ad.OnAdFullScreenContentClosed += () => EndRewarded();
                ad.OnAdFullScreenContentFailed += adError =>
                {
                    Debug.LogWarning("[Ludo] Rewarded ad failed to show: " + adError.GetMessage());
                    EndRewarded();
                };
            });
        }

        IEnumerator RetryRewardedLater()
        {
            float wait = Mathf.Min(15f * Mathf.Pow(2f, Mathf.Min(rewardedFailures - 1, 3)), 120f);
            yield return new WaitForSecondsRealtime(wait);
            LoadRewarded();
        }

        void ShowRewardedAd(Action<bool> onDone)
        {
            pendingRewarded = onDone ?? (_ => { });
            rewardEarned = false;
            rewarded.Show(reward =>
            {
                // the SDK says the player earned the reward. This can in principle be reported twice: it only counts once.
                if (rewardEarned) return;
                rewardEarned = true;
                Debug.Log("[Ludo] Rewarded ad: reward earned.");
            });
        }

        /// <summary>The rewarded ad closed (or failed). Tell the caller once whether the SDK confirmed the reward.</summary>
        void EndRewarded()
        {
            if (rewarded != null) { rewarded.Destroy(); rewarded = null; }      // an ad can be shown only once
            LoadRewarded();
            lastAdTime = Time.realtimeSinceStartup;      // the player just watched an ad: no interstitial right behind it
            gamesSinceLastAd = 0;
            var done = pendingRewarded;
            pendingRewarded = null;
            done?.Invoke(rewardEarned);
            rewardEarned = false;
        }

        IEnumerator RetryLoadLater()
        {
            // wait longer after each failure (15 s, 30 s, 60 s ...), never faster than Google recommends
            float wait = Mathf.Min(15f * Mathf.Pow(2f, Mathf.Min(loadFailures - 1, 3)), 120f);
            yield return new WaitForSecondsRealtime(wait);
            LoadInterstitial();
        }

        // ---------- showing ----------

        void TryShow(Action onContinue)
        {
            bool due = AdPolicy.ShouldShow(config, gamesFinished, gamesSinceLastAd, Time.realtimeSinceStartup - lastAdTime);
            if (!due || pendingContinue != null || interstitial == null || !interstitial.CanShowAd())
            {
                onContinue?.Invoke();
                return;
            }

            pendingContinue = onContinue ?? (() => { });
            gamesSinceLastAd = 0;
            lastAdTime = Time.realtimeSinceStartup;
            watchdog = StartCoroutine(WatchdogRoutine());
            interstitial.Show();
        }

        void OnAdClosed()
        {
            if (interstitial != null)
            {
                interstitial.Destroy();     // an ad can be shown only once; load a fresh one for next time
                interstitial = null;
            }
            LoadInterstitial();
            lastAdTime = Time.realtimeSinceStartup;      // count the time from when the player came back
            Continue();
        }

        IEnumerator WatchdogRoutine()
        {
            yield return new WaitForSecondsRealtime(AdShowWatchdogSeconds);
            watchdog = null;
            Debug.LogWarning("[Ludo] Interstitial did not report closing; continuing anyway.");
            Continue();
        }

        void Continue()
        {
            if (watchdog != null) { StopCoroutine(watchdog); watchdog = null; }
            var next = pendingContinue;
            pendingContinue = null;
            next?.Invoke();
        }
    }
}
