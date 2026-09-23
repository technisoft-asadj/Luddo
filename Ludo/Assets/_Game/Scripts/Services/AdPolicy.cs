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
    }
}
