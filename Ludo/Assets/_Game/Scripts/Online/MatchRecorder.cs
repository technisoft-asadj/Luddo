using System;
using System.Threading.Tasks;
using UnityEngine;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Settles one online match for THIS phone: works out the result (win / loss / forfeit / disconnect loss), asks RewardRules
    /// what it is worth, records it (StatsService) and hands the result screen a MatchSummary to display. It holds no
    /// reward numbers itself and draws nothing.
    ///
    /// Whether this match is ranked, and its id, come from THIS phone's own room state (RoomService.IsRanked / RoomId), not
    /// from the MatchStart the host broadcast: every client already knows both correctly from its own successful
    /// QuickMatchAsync/MergeIntoAsync, so there is nothing to trust over the wire for them.
    ///
    /// Only a Quick Match (ranked) changes Rank Points and can lose XP; a private room is casual (participation XP and coins).
    /// When a ranked match STARTS a provisional "disconnect loss" is written to the phone's pending list; a proper ending
    /// replaces it. So a player who cuts the connection or closes the app cannot avoid the loss.
    ///
    /// Honest note: the phone reports its own result. The trusted version (Cloud Code checking the match) is in PLAN.md.
    /// </summary>
    public sealed class MatchRecorder : IMatchSettler
    {
        const string LedgerKey = "ludo.bonus.claimed";

        readonly MatchStart plan;
        readonly int me;                 // my player number, -1 if I am not in the plan
        readonly bool ranked;
        readonly string matchId;
        readonly int fee;                // coin table entry (0 = free), from this phone's own room state
        readonly int people;             // real players at the table (the pot)
        bool finished;
        MatchSummary summary;

        public MatchRecorder(MatchStart plan, string myPlayerId)
        {
            this.plan = plan;
            me = Array.FindIndex(plan.seats, s => s.ai < 0 && s.playerId == myPlayerId);
            ranked = RoomService.IsRanked;
            matchId = RoomService.RoomId;
            fee = ranked ? RoomService.EntryFee : 0;
            people = Array.FindAll(plan.seats, s => s.ai < 0).Length;
            if (me >= 0 && ranked)                                    // ranked: leaving without a proper ending counts as a disconnect loss (and the entry is gone)
                PendingOutcomes.Set(MatchOutcome.From(RewardRules.Calculate(ResultKind.DisconnectLoss, MatchMode.Ranked), matchId, -fee));
        }

        MatchMode Mode => ranked ? MatchMode.Ranked : MatchMode.Casual;

        // ---------- how the match ended ----------

        /// <summary>
        /// The match has a winner. 'byForfeit': everybody else stopped playing (left, or did not reconnect), so the winner is the
        /// last person standing. A forfeit win is only paid when this phone can prove it is online itself (otherwise a player who
        /// cuts their own connection would win by "everybody left").
        /// </summary>
        public async Task<MatchSummary> Finished(int winner, bool winnerIsPerson, bool byForfeit)
        {
            if (finished || me < 0) return summary;
            finished = true;
            bool won = winner == me;

            if (byForfeit && won && !await RoomService.ReachableAsync())
                return Settle(ResultKind.DisconnectLoss, MatchMode.Ranked, forfeitWin: false);   // my own connection is gone: I did not win anything

            // a computer that took over a seat and won: the people still playing lose nothing (the people who left were already charged)
            if (!winnerIsPerson) return Settle(ResultKind.Loss, MatchMode.Casual, false, refund: true);
            return Settle(won ? ResultKind.Win : ResultKind.Loss, Mode, byForfeit && won);
        }

        /// <summary>I left before the end (pause menu, or away too long): a forfeit in a ranked match. A dropped connection is a DisconnectLoss.</summary>
        public MatchSummary Left(ResultKind kind)
        {
            if (finished || me < 0) return summary;
            finished = true;
            if (!ranked) return null;                                 // leaving a friendly game costs nothing
            return Settle(kind, MatchMode.Ranked, false);
        }

        /// <summary>The match cannot be settled (nobody can tell who is at fault): nothing changes for anybody.</summary>
        public void Void()
        {
            if (finished) return;
            finished = true;
            if (ranked) PendingOutcomes.Remove(matchId);
        }

        // ---------- the summary ----------

        MatchSummary Settle(ResultKind kind, MatchMode mode, bool forfeitWin, bool refund = false)
        {
            var payout = RewardRules.Calculate(kind, mode);
            // the coin table: the winner takes everybody's entry; a computer that won for a player who left refunds the people still playing
            int entry = fee <= 0 || refund ? 0 : CoinTables.Result(fee, people, kind == ResultKind.Win);
            var stats = StatsService.Mine;
            long xpBefore = stats.xp;
            long xpAfter = RewardRules.ApplyXp(xpBefore, payout.Xp);
            int rankBefore = stats.rating;
            int rankAfter = mode == MatchMode.Casual ? rankBefore : RewardRules.ApplyRank(rankBefore, payout.RankPoints);

            summary = new MatchSummary
            {
                MatchId = matchId,
                Result = kind,
                Mode = mode,
                ByForfeit = forfeitWin,
                RankBefore = rankBefore, RankAfter = rankAfter,
                TierBefore = Rating.Tier(rankBefore), TierAfter = Rating.Tier(rankAfter),
                XpBefore = xpBefore, XpAfter = xpAfter,
                LevelBefore = Progression.LevelFor(xpBefore), LevelAfter = Progression.LevelFor(xpAfter),
                Coins = payout.Coins + entry,
                AdBonusRank = IsBonusClaimed() ? 0 : RewardRules.AdBonus(payout)
            };
            if (mode != MatchMode.Casual && (kind == ResultKind.Win || kind == ResultKind.Loss))
                summary.WeeklyPoints = WeeklyCup.PointsFor(kind == ResultKind.Win);
            _ = StatsService.RecordAsync(MatchOutcome.From(payout, matchId, entry));      // replaces the provisional loss
            return summary;
        }

        // ---------- the optional rewarded-ad bonus (Rank Points only, once per match) ----------

        bool IsBonusClaimed() => BonusLedger.Parse(PlayerPrefs.GetString(LedgerKey, "")).IsClaimed(matchId);

        /// <summary>
        /// Call ONLY after the ads SDK confirmed the reward. Adds the bonus Rank Points once for this match; a second call does
        /// nothing (a duplicate callback cannot pay twice). Returns the updated summary.
        /// </summary>
        public MatchSummary ClaimAdBonus()
        {
            if (summary == null || !summary.CanOfferAd) return summary;
            var ledger = BonusLedger.Parse(PlayerPrefs.GetString(LedgerKey, ""));
            if (!ledger.TryClaim(matchId)) return summary;
            PlayerPrefs.SetString(LedgerKey, ledger.Serialize());
            PlayerPrefs.Save();

            int bonus = summary.AdBonusRank;
            summary.RankAfter += bonus;
            summary.TierAfter = Rating.Tier(summary.RankAfter);
            summary.AdBonusClaimed = true;
            _ = StatsService.AddRankBonusAsync(bonus);
            return summary;
        }
    }
}
