using System;
using System.Linq;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    public class RatingTests
    {
        [SetUp]
        public void DefaultSettings() => RewardSettings.Active = new RewardSettings();

        [TestCase(0, "Bronze")]
        [TestCase(499, "Bronze")]
        [TestCase(500, "Silver")]
        [TestCase(999, "Silver")]
        [TestCase(1000, "Gold")]
        [TestCase(2000, "Platinum")]
        [TestCase(3500, "Diamond")]
        [TestCase(5000, "Master")]
        [TestCase(99999, "Grandmaster")]
        public void RankFollowsRankPoints(int points, string rank) => Assert.AreEqual(rank, Rating.Tier(points));

        [Test]
        public void TheThresholdsComeFromTheSettings()
        {
            RewardSettings.Active = new RewardSettings { tiers = new[] { new RankTier("Wood", 0), new RankTier("Iron", 10) } };
            Assert.AreEqual("Wood", Rating.Tier(9));
            Assert.AreEqual("Iron", Rating.Tier(10));
            Assert.IsFalse(Rating.NextTier(10, out _, out _));           // the top rank has no next
        }

        [Test]
        public void NextRankAndProgressTowardsIt()
        {
            Assert.IsTrue(Rating.NextTier(1240, out string next, out int required));
            Assert.AreEqual("Platinum", next);
            Assert.AreEqual(2000, required);
            Assert.AreEqual((1240f - 1000f) / 1000f, Rating.TierFraction(1240), 1e-4f);
            Assert.AreEqual(1f, Rating.TierFraction(50000));
            Assert.AreEqual(0f, Rating.TierFraction(0));
        }

        [Test]
        public void NewPlayersStartAtTheConfiguredPoints()
        {
            Assert.AreEqual(0, Rating.Start);
            Assert.AreEqual(0, Rating.Floor);
            RewardSettings.Active = new RewardSettings { startRankPoints = 100, minRankPoints = 50 };
            Assert.AreEqual(100, Rating.Start);
            Assert.AreEqual(50, Rating.Floor);
        }

        [Test]
        public void NearbyRankPointsShareABand()
        {
            Assert.AreEqual(Rating.Band(0), Rating.Band(100));
            Assert.AreNotEqual(Rating.Band(0), Rating.Band(1000));
        }

        [Test]
        public void WeekStartsOnMondayUtc()
        {
            var wednesday = new DateTime(2026, 9, 23, 15, 30, 0, DateTimeKind.Utc);   // a Wednesday
            Assert.AreEqual(new DateTime(2026, 9, 21), WeeklyCup.WeekStart(wednesday));
            Assert.AreEqual(DayOfWeek.Monday, WeeklyCup.WeekStart(wednesday).DayOfWeek);
            var sunday = new DateTime(2026, 9, 27, 23, 59, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 9, 21), WeeklyCup.WeekStart(sunday));
            Assert.Less(WeeklyCup.TimeLeft(sunday), TimeSpan.FromMinutes(2));
        }

        [Test]
        public void WeekIdChangesOnlyOnMonday()
        {
            var sun = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
            var mon = new DateTime(2026, 9, 28, 0, 0, 1, DateTimeKind.Utc);
            Assert.AreNotEqual(WeeklyCup.WeekId(sun), WeeklyCup.WeekId(mon));
            Assert.AreEqual(WeeklyCup.WeekId(mon), WeeklyCup.WeekId(mon.AddDays(5)));
        }

        [Test]
        public void WinningIsWorthMoreThanPlaying() =>
            Assert.Greater(WeeklyCup.PointsFor(true), WeeklyCup.PointsFor(false));

        static readonly DateTime Wed = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

        static Ludo.Online.MatchOutcome Out(ResultKind kind, MatchMode mode, string id = "") =>
            Ludo.Online.MatchOutcome.From(RewardRules.Calculate(kind, mode), id);

        [Test]
        public void ARankedWinMovesRankPointsStreakAndCupPoints()
        {
            var s = new Ludo.Online.PlayerStats();
            Ludo.Online.StatsService.Apply(s, Out(ResultKind.Win, MatchMode.Ranked), Wed);
            Assert.AreEqual(25, s.rating);
            Assert.AreEqual(1, s.games); Assert.AreEqual(1, s.wins);
            Assert.AreEqual(1, s.rankedGames); Assert.AreEqual(1, s.rankedWins);
            Assert.AreEqual(1, s.streak); Assert.AreEqual(1, s.bestStreak);
            Assert.AreEqual(WeeklyCup.WinPoints, s.weeklyPoints);
        }

        [Test]
        public void ALossEndsTheStreakButKeepsTheBest()
        {
            var s = new Ludo.Online.PlayerStats();
            for (int i = 0; i < 3; i++) Ludo.Online.StatsService.Apply(s, Out(ResultKind.Win, MatchMode.Casual), Wed);
            Ludo.Online.StatsService.Apply(s, Out(ResultKind.Loss, MatchMode.Casual), Wed);
            Assert.AreEqual(0, s.streak);
            Assert.AreEqual(3, s.bestStreak);
            Assert.AreEqual(0, s.rating);               // a private room never touches Rank Points
            Assert.AreEqual(0, s.weeklyPoints);
        }

        [Test]
        public void LeavingARankedMatchCostsRankPointsButEarnsNoCupPoints()
        {
            var s = new Ludo.Online.PlayerStats { rating = 100 };
            Ludo.Online.StatsService.Apply(s, Out(ResultKind.Forfeit, MatchMode.Ranked), Wed);
            Assert.AreEqual(85, s.rating);
            Assert.AreEqual(0, s.weeklyPoints);
        }

        [Test]
        public void TheWeeklyCupStartsOverOnMonday()
        {
            var s = new Ludo.Online.PlayerStats();
            Ludo.Online.StatsService.Apply(s, Out(ResultKind.Win, MatchMode.Ranked), Wed);
            Assert.AreEqual(3, s.weeklyPoints);
            Ludo.Online.StatsService.Apply(s, Out(ResultKind.Loss, MatchMode.Ranked), Wed.AddDays(6));   // next Tuesday
            Assert.AreEqual(1, s.weeklyPoints);
        }
    }
}
