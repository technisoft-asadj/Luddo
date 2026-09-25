using System;
using NUnit.Framework;
using Ludo.Core;
using Ludo.Online;

namespace Ludo.Tests
{
    /// <summary>Coin tables (entry fee, winner takes the pot) and the 7-day daily reward.</summary>
    public class CoinsTests
    {
        [Test]
        public void EveryGameModeSurvivesTheRoomProperty()
        {
            foreach (GameMode m in Enum.GetValues(typeof(GameMode)))
                Assert.AreEqual(m, RoomService.ModeFrom((int)m), m + " must come back from the room as itself");
            Assert.AreEqual(GameMode.Classic, RoomService.ModeFrom(99));
            Assert.AreEqual(GameMode.Classic, RoomService.ModeFrom(-1));
        }

        [Test]
        public void OnlineTablesAreNeverFree()
        {
            foreach (var fee in CoinTables.OnlineFees) Assert.Greater(fee, 0);
            Assert.IsTrue(Array.IndexOf(CoinTables.OnlineFees, CoinTables.DefaultFee) >= 0);
            Assert.AreEqual(300, CoinTables.Prize(100, 4));
            Assert.AreEqual(100, CoinTables.Prize(100, 2));
            Assert.IsFalse(CoinTables.CanAfford(99, CoinTables.DefaultFee));
            Assert.IsTrue(CoinTables.CanAfford(CoinTables.StarterCoins, CoinTables.DefaultFee));
        }

        [Test]
        public void WinnerTakesTheOtherEntries()
        {
            Assert.AreEqual(300, CoinTables.Result(100, 4, true));
            Assert.AreEqual(-100, CoinTables.Result(100, 4, false));
            Assert.AreEqual(500, CoinTables.Result(500, 2, true));
            Assert.AreEqual(0, CoinTables.Result(0, 4, true), "free table");
            Assert.AreEqual(400, CoinTables.Pot(100, 4));
            Assert.IsTrue(CoinTables.IsFee(1000));
            Assert.IsFalse(CoinTables.IsFee(123));
            Assert.AreEqual("5K", CoinTables.Label(5000));
            Assert.AreEqual("Free", CoinTables.Label(0));
            Assert.IsFalse(CoinTables.CanAfford(99, 100));
        }

        [Test]
        public void EntryIsAppliedAndCoinsNeverGoNegative()
        {
            var s = new PlayerStats { coins = 150 };
            StatsService.Apply(s, new MatchOutcome { matchId = "a", result = ResultKind.Win, coins = 50, entryCoins = 300 }, DateTime.UtcNow);
            Assert.AreEqual(500, s.coins);
            StatsService.Apply(s, new MatchOutcome { matchId = "b", result = ResultKind.Loss, coins = 0, entryCoins = -1000 }, DateTime.UtcNow);
            Assert.AreEqual(0, s.coins);
        }

        [Test]
        public void DailyCalendarMovesOnAndResets()
        {
            var today = new DateTime(2026, 9, 24);
            int yesterday = DailyReward.DayKey(today.AddDays(-1));
            Assert.AreEqual(1, DailyReward.NextDay(0, 0, today), "first ever claim");
            Assert.AreEqual(4, DailyReward.NextDay(yesterday, 3, today), "came back the next day");
            Assert.AreEqual(1, DailyReward.NextDay(DailyReward.DayKey(today.AddDays(-2)), 3, today), "missed a day");
            Assert.AreEqual(1, DailyReward.NextDay(yesterday, 7, today), "after day 7 it starts over");
            Assert.IsFalse(DailyReward.CanClaim(DailyReward.DayKey(today), today), "once a day");
            Assert.IsTrue(DailyReward.CanClaim(yesterday, today));
            Assert.AreEqual(100, DailyReward.AmountFor(1));
            Assert.AreEqual(1500, DailyReward.AmountFor(7));
            Assert.AreEqual(20260924, DailyReward.DayKey(today));
        }
    }
}
