namespace Ludo.Services
{
    /// <summary>
    /// The "may we show an ad now?" rule, kept as plain arithmetic (no SDK, no Unity) so it can be unit-tested.
    /// An interstitial is allowed only when the player has finished enough games in this session, enough games have
    /// passed since the last ad, and enough time has passed since the last ad.
    /// </summary>
    public static class AdPolicy
    {
        public static bool ShouldShow(AdsConfig config, int gamesFinished, int gamesSinceLastAd, float secondsSinceLastAd)
        {
            if (config == null || !config.adsEnabled) return false;
            if (gamesFinished < config.firstAdAfterGames) return false;
            if (gamesSinceLastAd < config.minGamesBetweenAds) return false;
            if (secondsSinceLastAd < config.minSecondsBetweenAds) return false;
            return true;
        }

        /// <summary>
        /// May an App Open ad be shown as the player comes back to the app? Only after a real absence, not too soon after
        /// the last App Open ad or any other ad, and never while the player is in a game or a room ('blocked').
        /// </summary>
        public static bool ShouldShowAppOpen(AdsConfig config, float secondsAway, float secondsSinceLastAppOpen, float secondsSinceAnyAd, bool blocked)
        {
            if (config == null || !config.adsEnabled || !config.showAppOpenAd || blocked) return false;
            if (secondsAway < config.appOpenMinAwaySeconds) return false;
            if (secondsSinceLastAppOpen < config.appOpenMinSecondsBetween) return false;
            if (secondsSinceAnyAd < 10f) return false;           // coming back from an interstitial or rewarded ad is not a return to the app
            return true;
        }
    }
}
