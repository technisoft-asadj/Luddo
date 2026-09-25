using System;

namespace Ludo.Core
{
    /// <summary>
    /// The free chest: a small pile of coins that fills up again after a few hours, so there is always a way back for a
    /// player who has spent everything. It is free, it is never sold for real money, and watching an ad is only ever an
    /// optional way to take a second one - never a condition for the first.
    ///
    /// Pure arithmetic over "the minute the last chest was opened", so it is fully testable and the clock is always passed
    /// in rather than read here.
    /// </summary>
    public static class Chest
    {
        /// <summary>Hours between free chests.</summary>
        public const int IntervalHours = 4;

        /// <summary>The smallest and largest coin pile a chest can hold.</summary>
        public const int MinCoins = 60, MaxCoins = 260;

        /// <summary>A player with fewer coins than this may watch an ad for <see cref="AdCoins"/> more (never a condition for anything).</summary>
        public const int BrokeBelow = 100, AdCoins = 100;

        public static bool CanWatchForCoins(int coins) => coins < BrokeBelow;

        /// <summary>Minutes since 2020-01-01 UTC - small enough for an int, and time-zone proof (unlike the daily reward's day key).</summary>
        public static int Stamp(DateTime utcNow) => (int)(utcNow.ToUniversalTime() - new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMinutes;

        /// <summary>Minutes still to wait (0 = the chest is ready). A stamp from the future is treated as "ready" rather than trusted.</summary>
        public static int MinutesLeft(int lastOpened, DateTime utcNow)
        {
            if (lastOpened <= 0) return 0;                         // never opened: the first chest is waiting
            int now = Stamp(utcNow);
            if (lastOpened > now) return 0;                        // a clock that went backwards must not lock the chest forever
            int done = lastOpened + IntervalHours * 60;
            return done <= now ? 0 : done - now;
        }

        public static bool IsReady(int lastOpened, DateTime utcNow) => MinutesLeft(lastOpened, utcNow) == 0;

        /// <summary>"Ready!" or "2h 14m" - what the button says.</summary>
        public static string Countdown(int lastOpened, DateTime utcNow)
        {
            int left = MinutesLeft(lastOpened, utcNow);
            if (left <= 0) return "Ready!";
            int hours = left / 60, minutes = left % 60;
            return hours > 0 ? hours + "h " + minutes + "m" : minutes + "m";
        }

        /// <summary>
        /// What is inside. The size is drawn from the seed the caller passes (the open stamp), so the same open always pays
        /// the same amount however often the screen is redrawn, and nothing about the player changes the odds.
        /// </summary>
        public static int CoinsFor(int seed)
        {
            var rng = new Random(seed);
            return MinCoins + rng.Next(MaxCoins - MinCoins + 1);
        }
    }
}
