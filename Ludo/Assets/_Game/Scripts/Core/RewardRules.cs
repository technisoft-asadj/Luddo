using System;
using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>How a match ended for one player.</summary>
    public enum ResultKind
    {
        Win,                // won (also "win by forfeit": see MatchSummary.ByForfeit)
        Loss,               // lost the race
        Forfeit,            // left the match (pause menu, or was away too long while still connected)
        DisconnectLoss      // the connection dropped and did not come back in time
    }

    /// <summary>What kind of match it was. Only Ranked and Tournament move Rank Points; a private room with friends is Casual.</summary>
    public enum MatchMode { Casual, Ranked, Tournament }

    /// <summary>What one match pays (or takes) for one player, before floors are applied. Positive = gained, negative = lost.</summary>
    public readonly struct MatchPayout
    {
        public readonly ResultKind Result;
        public readonly MatchMode Mode;
        public readonly int RankPoints;
        public readonly int Xp;
        public readonly int Coins;

        public MatchPayout(ResultKind result, MatchMode mode, int rankPoints, int xp, int coins)
        {
            Result = result; Mode = mode; RankPoints = rankPoints; Xp = xp; Coins = coins;
        }

        public bool IsWin => Result == ResultKind.Win;
    }

    /// <summary>
    /// The reward maths of an online match, pure and testable: no UI, no cloud. The flow is
    ///   result -> Calculate (payout) -> apply to the profile (ApplyRank / ApplyXp) -> optional ad bonus (AdBonus) -> rank and level updates.
    /// Every number comes from RewardSettings.
    /// </summary>
    public static class RewardRules
    {
        /// <summary>What this result is worth. A win pays Rank Points, XP and coins; a loss or forfeit takes Rank Points and XP (never coins).</summary>
        public static MatchPayout Calculate(ResultKind result, MatchMode mode, RewardSettings s = null)
        {
            s = s ?? RewardSettings.Active;
            if (mode == MatchMode.Casual)
                return result == ResultKind.Win
                    ? new MatchPayout(result, mode, 0, s.casualWinXp, s.casualWinCoins)
                    : new MatchPayout(result, mode, 0, s.casualLossXp, s.casualLossCoins);     // (no penalty for leaving a friendly game)

            switch (result)
            {
                case ResultKind.Win:
                    return mode == MatchMode.Tournament
                        ? new MatchPayout(result, mode, s.tournamentWinRankPoints, s.tournamentWinXp + s.participationXp, s.tournamentWinCoins)
                        : new MatchPayout(result, mode, s.winRankPoints, s.winXp + s.participationXp, s.winCoins);
                case ResultKind.Loss:
                    return new MatchPayout(result, mode, -s.lossRankPoints, s.participationXp - s.lossXp, s.lossCoins);
                default:                                            // Forfeit, DisconnectLoss
                    return new MatchPayout(result, mode, -s.forfeitRankPoints, -s.forfeitXp, 0);
            }
        }

        /// <summary>Rank Points after a change, never under the minimum.</summary>
        public static int ApplyRank(int current, int change, RewardSettings s = null)
        {
            s = s ?? RewardSettings.Active;
            return Math.Max(s.minRankPoints, current + change);
        }

        /// <summary>Total XP after a change. It never goes below 0, and (unless the settings allow it) never below the start of the level the player is on.</summary>
        public static long ApplyXp(long current, int change, RewardSettings s = null)
        {
            s = s ?? RewardSettings.Active;
            long result = current + change;
            long floor = s.xpLossCanLowerLevel ? 0 : Progression.TotalXpFor(Progression.LevelFor(current));
            if (change < 0) result = Math.Max(result, floor);
            return Math.Max(0, result);
        }

        /// <summary>
        /// The extra Rank Points a rewarded ad adds to a WIN: the win's Rank Points times (multiplier - 1), so the total is
        /// "multiplier x". Only Rank Points: XP and coins are never multiplied. Nothing for losses, forfeits or casual games.
        /// </summary>
        public static int AdBonus(MatchPayout payout, RewardSettings s = null)
        {
            s = s ?? RewardSettings.Active;
            if (!payout.IsWin || payout.Mode == MatchMode.Casual || payout.RankPoints <= 0) return 0;
            return payout.RankPoints * Math.Max(0, s.adRankMultiplier - 1);
        }
    }

    /// <summary>Everything the result screen shows about one finished match. Built by the online code; the screen only displays it.</summary>
    public sealed class MatchSummary
    {
        public string MatchId = "";
        public ResultKind Result;
        public MatchMode Mode;
        public bool ByForfeit;                  // won because the opponent(s) did not come back
        public bool Recorded = true;            // false: nothing was saved (the match could not be settled)

        public int RankBefore, RankAfter;       // the ACTUAL change (RankAfter - RankBefore) can be smaller than the payout when the minimum is hit
        public string TierBefore = "", TierAfter = "";
        public long XpBefore, XpAfter;
        public int LevelBefore, LevelAfter;
        public int Coins;
        public int WeeklyPoints;

        public int AdBonusRank;                 // extra Rank Points the ad would give (0 = no offer)
        public bool AdBonusClaimed;             // already added: the ad cannot be offered again for this match

        public int RankChange => RankAfter - RankBefore;
        public int XpChange => (int)(XpAfter - XpBefore);
        public bool RankUp => Rating.TierIndex(RankAfter) > Rating.TierIndex(RankBefore);
        public bool RankDown => Rating.TierIndex(RankAfter) < Rating.TierIndex(RankBefore);
        public bool LevelUp => LevelAfter > LevelBefore;
        public bool CanOfferAd => AdBonusRank > 0 && !AdBonusClaimed;
    }

    /// <summary>Remembers which matches already had their ad bonus, so it can be claimed only once per match (also if the ad reports twice).</summary>
    public sealed class BonusLedger
    {
        readonly List<string> claimed = new List<string>();
        readonly int capacity;

        public BonusLedger(int capacity = 40) { this.capacity = capacity; }

        public bool IsClaimed(string matchId) => claimed.Contains(matchId);

        /// <summary>True the first time for this match; false every time after.</summary>
        public bool TryClaim(string matchId)
        {
            if (string.IsNullOrEmpty(matchId) || claimed.Contains(matchId)) return false;
            claimed.Add(matchId);
            if (claimed.Count > capacity) claimed.RemoveAt(0);
            return true;
        }

        public string Serialize() => string.Join(",", claimed);

        public static BonusLedger Parse(string text, int capacity = 40)
        {
            var ledger = new BonusLedger(capacity);
            if (!string.IsNullOrEmpty(text))
                foreach (var id in text.Split(',')) if (id.Length > 0) ledger.claimed.Add(id);
            return ledger;
        }
    }
}
