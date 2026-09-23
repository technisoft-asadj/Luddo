using System;

namespace Ludo.Core
{
    /// <summary>One competitive rank: its name and the Rank Points a player needs to hold it.</summary>
    [Serializable]
    public sealed class RankTier
    {
        public string name = "";
        public int minPoints;
        public RankTier() { }
        public RankTier(string name, int minPoints) { this.name = name; this.minPoints = minPoints; }
    }

    /// <summary>
    /// Every number of the ranking, XP and reward system in ONE place (pure data, no Unity). The game reads them from
    /// RewardSettings.Active; the online code fills Active from the "RewardConfig" asset in Resources, so all of these
    /// can be tuned in the Inspector without touching code. The UI never contains a reward number.
    /// </summary>
    [Serializable]
    public sealed class RewardSettings
    {
        // ---------- Rank Points and ranks ----------
        public int startRankPoints = 0;
        public int minRankPoints = 0;                       // Rank Points never go below this
        public int matchmakingBandWidth = 250;              // players this many Rank Points apart share a matchmaking band
        public RankTier[] tiers =
        {
            new RankTier("Bronze", 0), new RankTier("Silver", 500), new RankTier("Gold", 1000), new RankTier("Platinum", 2000),
            new RankTier("Diamond", 3500), new RankTier("Master", 5000), new RankTier("Grandmaster", 7500)
        };

        // ---------- ranked match (Quick Match) ----------
        public int winRankPoints = 25;
        public int lossRankPoints = 15;                     // subtracted
        public int winXp = 100;
        public int lossXp = 25;                             // subtracted
        public int participationXp = 0;                     // added to every ranked result, win or lose
        public int winCoins = 50;
        public int lossCoins = 0;

        // ---------- leaving / not reconnecting in a ranked match ----------
        public int forfeitRankPoints = 15;                  // subtracted
        public int forfeitXp = 25;                          // subtracted
        public float reconnectGraceSeconds = 20f;           // how long a dropped player has to come back before they forfeit

        // ---------- rewarded ad ----------
        public int adRankMultiplier = 2;                    // Rank Points of a win x this (XP and coins are NOT multiplied)

        // ---------- private rooms with friends: casual, no Rank Points ----------
        public int casualWinXp = 60;
        public int casualLossXp = 20;
        public int casualWinCoins = 40;
        public int casualLossCoins = 10;

        // ---------- tournaments (paid out by the tournament system through RewardRules) ----------
        public int tournamentWinRankPoints = 40;
        public int tournamentWinXp = 150;
        public int tournamentWinCoins = 100;

        // ---------- XP ----------
        public bool xpLossCanLowerLevel = false;            // false: a loss can empty the bar of the current level but never lower the level

        /// <summary>The values the game uses. Replaced at start-up by the RewardConfig asset (if there is one).</summary>
        public static RewardSettings Active = new RewardSettings();

        public RewardSettings Copy() => (RewardSettings)MemberwiseClone();
    }
}
