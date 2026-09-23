using System;
using NUnit.Framework;
using Ludo.Core;
using Ludo.Online;

namespace Ludo.Tests
{
    public class RewardRulesTests
    {
        static readonly DateTime Now = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void DefaultSettings() => RewardSettings.Active = new RewardSettings();

        // ---------- what each result pays ----------

        [Test]
        public void ARankedWinPaysRankPointsXpAndCoins()
        {
            var p = RewardRules.Calculate(ResultKind.Win, MatchMode.Ranked);
            Assert.AreEqual(25, p.RankPoints);
            Assert.AreEqual(100, p.Xp);
            Assert.AreEqual(50, p.Coins);
        }

        [Test]
        public void ARankedLossTakesRankPointsAndXpButNeverCoins()
        {
            var p = RewardRules.Calculate(ResultKind.Loss, MatchMode.Ranked);
            Assert.AreEqual(-15, p.RankPoints);
            Assert.AreEqual(-25, p.Xp);
            Assert.AreEqual(0, p.Coins);
        }

        [Test]
        public void AForfeitAndADisconnectLoseLikeALoss()
        {
            foreach (var kind in new[] { ResultKind.Forfeit, ResultKind.DisconnectLoss })
            {
                var p = RewardRules.Calculate(kind, MatchMode.Ranked);
                Assert.AreEqual(-15, p.RankPoints, kind.ToString());
                Assert.AreEqual(-25, p.Xp, kind.ToString());
                Assert.AreEqual(0, p.Coins, kind.ToString());
            }
        }

        [Test]
        public void EveryNumberComesFromTheSettings()
        {
            var s = new RewardSettings { winRankPoints = 40, lossRankPoints = 8, winXp = 300, lossXp = 10, participationXp = 5, winCoins = 7, forfeitRankPoints = 30, forfeitXp = 60 };
            Assert.AreEqual(40, RewardRules.Calculate(ResultKind.Win, MatchMode.Ranked, s).RankPoints);
            Assert.AreEqual(305, RewardRules.Calculate(ResultKind.Win, MatchMode.Ranked, s).Xp);       // win XP + participation
            Assert.AreEqual(7, RewardRules.Calculate(ResultKind.Win, MatchMode.Ranked, s).Coins);
            Assert.AreEqual(-8, RewardRules.Calculate(ResultKind.Loss, MatchMode.Ranked, s).RankPoints);
            Assert.AreEqual(-5, RewardRules.Calculate(ResultKind.Loss, MatchMode.Ranked, s).Xp);       // participation 5 - loss 10
            Assert.AreEqual(-30, RewardRules.Calculate(ResultKind.Forfeit, MatchMode.Ranked, s).RankPoints);
            Assert.AreEqual(-60, RewardRules.Calculate(ResultKind.DisconnectLoss, MatchMode.Ranked, s).Xp);
        }

        [Test]
        public void APrivateRoomNeverMovesRankPointsAndNeverCostsXp()
        {
            foreach (var kind in new[] { ResultKind.Win, ResultKind.Loss, ResultKind.Forfeit })
            {
                var p = RewardRules.Calculate(kind, MatchMode.Casual);
                Assert.AreEqual(0, p.RankPoints, kind.ToString());
                Assert.GreaterOrEqual(p.Xp, 0, kind.ToString());
                Assert.AreEqual(0, RewardRules.AdBonus(p), kind.ToString());
            }
        }

        [Test]
        public void ATournamentWinUsesItsOwnRewards()
        {
            var p = RewardRules.Calculate(ResultKind.Win, MatchMode.Tournament);
            Assert.AreEqual(40, p.RankPoints);
            Assert.AreEqual(150, p.Xp);
            Assert.AreEqual(100, p.Coins);
        }

        // ---------- floors ----------

        [Test]
        public void RankPointsNeverGoBelowTheMinimum()
        {
            Assert.AreEqual(0, RewardRules.ApplyRank(10, -15));
            Assert.AreEqual(0, RewardRules.ApplyRank(0, -15));
            Assert.AreEqual(10, RewardRules.ApplyRank(25, -15));
            RewardSettings.Active = new RewardSettings { minRankPoints = 100 };
            Assert.AreEqual(100, RewardRules.ApplyRank(105, -15));
        }

        [Test]
        public void XpNeverGoesBelowZeroAndByDefaultNeverLowersTheLevel()
        {
            Assert.AreEqual(0, RewardRules.ApplyXp(10, -25));
            // level 2 starts at 100 XP: a loss cannot push a level 2 player back to level 1
            Assert.AreEqual(100, RewardRules.ApplyXp(110, -25));
            Assert.AreEqual(2, Progression.LevelFor(RewardRules.ApplyXp(110, -25)));
            Assert.AreEqual(150, RewardRules.ApplyXp(175, -25));
            RewardSettings.Active = new RewardSettings { xpLossCanLowerLevel = true };
            Assert.AreEqual(85, RewardRules.ApplyXp(110, -25));
        }

        [Test]
        public void XpAndRankPointsAreSeparate()
        {
            var s = new PlayerStats();
            StatsService.Apply(s, MatchOutcome.From(RewardRules.Calculate(ResultKind.Win, MatchMode.Ranked), "a"), Now);
            Assert.AreEqual(25, s.rating);
            Assert.AreEqual(100, s.xp);
            Assert.AreEqual(2, s.Level);                                 // 100 XP is level 2 ...
            Assert.AreEqual("Bronze", s.Tier);                           // ... while 25 Rank Points is still Bronze
        }

        [Test]
        public void ALossCanRankDownAndKeepsRankPointsAtZero()
        {
            var s = new PlayerStats { rating = 10, xp = 0 };
            StatsService.Apply(s, MatchOutcome.From(RewardRules.Calculate(ResultKind.Loss, MatchMode.Ranked), "b"), Now);
            Assert.AreEqual(0, s.rating);
            Assert.AreEqual(0, s.xp);
        }

        // ---------- the rewarded ad ----------

        [Test]
        public void TheAdDoublesOnlyTheRankPointsOfAWin()
        {
            var win = RewardRules.Calculate(ResultKind.Win, MatchMode.Ranked);
            Assert.AreEqual(25, RewardRules.AdBonus(win));               // +25 -> +50 in total
            Assert.AreEqual(0, RewardRules.AdBonus(RewardRules.Calculate(ResultKind.Loss, MatchMode.Ranked)));
            Assert.AreEqual(0, RewardRules.AdBonus(RewardRules.Calculate(ResultKind.Forfeit, MatchMode.Ranked)));
            Assert.AreEqual(0, RewardRules.AdBonus(RewardRules.Calculate(ResultKind.DisconnectLoss, MatchMode.Ranked)));
        }

        [Test]
        public void TheAdMultiplierIsConfigurable()
        {
            var s = new RewardSettings { adRankMultiplier = 3 };
            Assert.AreEqual(50, RewardRules.AdBonus(RewardRules.Calculate(ResultKind.Win, MatchMode.Ranked, s), s));
            s.adRankMultiplier = 1;                                      // 1x = no bonus
            Assert.AreEqual(0, RewardRules.AdBonus(RewardRules.Calculate(ResultKind.Win, MatchMode.Ranked, s), s));
        }

        [Test]
        public void TheBonusCanBeClaimedOnlyOncePerMatch()
        {
            var ledger = new BonusLedger();
            Assert.IsTrue(ledger.TryClaim("match1"));
            Assert.IsFalse(ledger.TryClaim("match1"));                   // a second (or duplicate) claim pays nothing
            Assert.IsTrue(ledger.TryClaim("match2"));
            Assert.IsFalse(ledger.TryClaim(""));                         // no match id: no claim
        }

        [Test]
        public void TheLedgerSurvivesBeingSavedAndRead()
        {
            var ledger = new BonusLedger();
            ledger.TryClaim("a"); ledger.TryClaim("b");
            var again = BonusLedger.Parse(ledger.Serialize());
            Assert.IsTrue(again.IsClaimed("a"));
            Assert.IsFalse(again.TryClaim("b"));
        }

        [Test]
        public void TheLedgerForgetsOnlyTheOldestMatches()
        {
            var ledger = new BonusLedger(2);
            ledger.TryClaim("a"); ledger.TryClaim("b"); ledger.TryClaim("c");
            Assert.IsFalse(ledger.IsClaimed("a"));
            Assert.IsTrue(ledger.IsClaimed("c"));
        }

        [Test]
        public void ASummaryOnlyOffersTheAdOnce()
        {
            var summary = new MatchSummary { Result = ResultKind.Win, Mode = MatchMode.Ranked, AdBonusRank = 25 };
            Assert.IsTrue(summary.CanOfferAd);
            summary.AdBonusClaimed = true;
            Assert.IsFalse(summary.CanOfferAd);
        }

        // ---------- rank up / level up are separate events ----------

        [Test]
        public void RankUpAndLevelUpAreDetectedSeparately()
        {
            var summary = new MatchSummary { RankBefore = 980, RankAfter = 1030, XpBefore = 50, XpAfter = 150, LevelBefore = 1, LevelAfter = 2 };
            Assert.IsTrue(summary.RankUp);                               // Silver -> Gold
            Assert.IsTrue(summary.LevelUp);
            var onlyLevel = new MatchSummary { RankBefore = 10, RankAfter = 35, LevelBefore = 1, LevelAfter = 2 };
            Assert.IsFalse(onlyLevel.RankUp);
            Assert.IsTrue(onlyLevel.LevelUp);
            var onlyRank = new MatchSummary { RankBefore = 990, RankAfter = 1005, LevelBefore = 5, LevelAfter = 5 };
            Assert.IsTrue(onlyRank.RankUp);
            Assert.IsFalse(onlyRank.LevelUp);
        }

        // ---------- bookkeeping ----------

        [Test]
        public void ADisconnectLossIsCountedAndRecordedOnlyOnce()
        {
            var s = new PlayerStats { rating = 100 };
            var outcome = MatchOutcome.From(RewardRules.Calculate(ResultKind.DisconnectLoss, MatchMode.Ranked), "m1");
            StatsService.Apply(s, outcome, Now);
            Assert.AreEqual(1, s.disconnects);
            Assert.AreEqual(1, s.Losses);
            Assert.AreEqual(100, s.DisconnectRatePercent);
            Assert.AreEqual(85, s.rating);
            Assert.AreEqual("m1", s.recent);
        }

        [Test]
        public void ThePendingListKeepsOneResultPerMatch()
        {
            PlayerPrefs_Clear();
            var provisional = MatchOutcome.From(RewardRules.Calculate(ResultKind.DisconnectLoss, MatchMode.Ranked), "m2");
            PendingOutcomes.Set(provisional);
            Assert.AreEqual(1, PendingOutcomes.Count);
            PendingOutcomes.Set(MatchOutcome.From(RewardRules.Calculate(ResultKind.Win, MatchMode.Ranked), "m2"));   // the real ending replaces it
            Assert.AreEqual(1, PendingOutcomes.Count);
            Assert.AreEqual(ResultKind.Win, PendingOutcomes.All()[0].result);
            PendingOutcomes.Remove("m2");
            Assert.AreEqual(0, PendingOutcomes.Count);
            PlayerPrefs_Clear();
        }

        [Test]
        public void ThePendingBonusAddsUpAndClears()
        {
            PlayerPrefs_Clear();
            PendingOutcomes.AddBonus(25);
            PendingOutcomes.AddBonus(10);
            Assert.AreEqual(35, PendingOutcomes.Bonus);
            PendingOutcomes.ClearBonus(35);
            Assert.AreEqual(0, PendingOutcomes.Bonus);
            PlayerPrefs_Clear();
        }

        static void PlayerPrefs_Clear()
        {
            UnityEngine.PlayerPrefs.DeleteKey("ludo.pending.outcomes");
            UnityEngine.PlayerPrefs.DeleteKey("ludo.pending.bonus");
        }
    }
}
