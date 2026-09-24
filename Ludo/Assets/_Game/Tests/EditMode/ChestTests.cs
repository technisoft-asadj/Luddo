using System;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    /// <summary>The free chest: when it is ready, what it says, and that it always pays something.</summary>
    public class ChestTests
    {
        static readonly DateTime Now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void TheFirstChestIsWaiting()
        {
            Assert.IsTrue(Chest.IsReady(0, Now));
            Assert.AreEqual("Ready!", Chest.Countdown(0, Now));
        }

        [Test]
        public void ItLocksForTheWholeIntervalAndThenOpens()
        {
            int opened = Chest.Stamp(Now);
            Assert.IsFalse(Chest.IsReady(opened, Now));
            Assert.IsFalse(Chest.IsReady(opened, Now.AddHours(Chest.IntervalHours).AddMinutes(-1)));
            Assert.IsTrue(Chest.IsReady(opened, Now.AddHours(Chest.IntervalHours)));
            Assert.IsTrue(Chest.IsReady(opened, Now.AddDays(3)));
        }

        [Test]
        public void TheCountdownCountsDown()
        {
            int opened = Chest.Stamp(Now);
            Assert.AreEqual(Chest.IntervalHours * 60, Chest.MinutesLeft(opened, Now));
            Assert.AreEqual("1h 30m", Chest.Countdown(opened, Now.AddHours(Chest.IntervalHours - 1.5)));
            Assert.AreEqual("45m", Chest.Countdown(opened, Now.AddHours(Chest.IntervalHours).AddMinutes(-45)));
            Assert.AreEqual("Ready!", Chest.Countdown(opened, Now.AddHours(Chest.IntervalHours)));
        }

        [Test]
        public void AClockThatWentBackwardsDoesNotLockTheChestForever()
        {
            int fromTheFuture = Chest.Stamp(Now.AddDays(30));
            Assert.IsTrue(Chest.IsReady(fromTheFuture, Now), "a stamp ahead of now must not be trusted");
        }

        [Test]
        public void EveryChestPaysSomethingInsideTheAdvertisedRange()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                int coins = Chest.CoinsFor(seed);
                Assert.GreaterOrEqual(coins, Chest.MinCoins, "seed " + seed);
                Assert.LessOrEqual(coins, Chest.MaxCoins, "seed " + seed);
            }
        }

        [Test]
        public void TheSameOpenAlwaysPaysTheSameAmount()
        {
            Assert.AreEqual(Chest.CoinsFor(12345), Chest.CoinsFor(12345), "redrawing the screen must not reroll the reward");
            Assert.AreNotEqual(Chest.CoinsFor(1), Chest.CoinsFor(2), "different opens should differ");
        }

        [Test]
        public void TheStampMovesOneStepPerMinute()
        {
            Assert.AreEqual(Chest.Stamp(Now) + 60, Chest.Stamp(Now.AddHours(1)));
            Assert.Greater(Chest.Stamp(Now), 0);
        }
    }
}
