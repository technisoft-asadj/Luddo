using System;

namespace Ludo.Core
{
    /// <summary>
    /// The coin tables (like Ludo Star): every player at a Quick Match table pays the same entry, the winner takes the pot.
    /// Coins are virtual only: earned by playing, the daily reward and rewarded ads; they can never be bought or cashed out.
    /// Pure rules, unit-tested; the online code applies them.
    /// </summary>
    public static class CoinTables
    {
        /// <summary>The entry fees a player can choose (0 = a free table).</summary>
        public static readonly int[] Fees = { 0, 100, 500, 1000, 5000 };

        /// <summary>The tables online players can sit at: like Ludo Star every online match costs an entry, there is no free table.</summary>
        public static readonly int[] OnlineFees = { 100, 500, 1000, 5000 };

        /// <summary>The smallest entry a player can pick; what a saved "free" choice becomes.</summary>
        public const int DefaultFee = 100;

        /// <summary>What the winner of a table takes home, not counting their own entry.</summary>
        public static int Prize(int fee, int players) => Math.Max(0, fee) * Math.Max(0, players - 1);

        /// <summary>Coins a new profile starts with.</summary>
        public const int StarterCoins = 1000;

        public static bool IsFee(int fee) => Array.IndexOf(Fees, fee) >= 0;

        public static int Pot(int fee, int players) => Math.Max(0, fee) * Math.Max(0, players);

        /// <summary>What the table does to my coins: the winner gets everybody else's entry, everybody else loses theirs.</summary>
        public static int Result(int fee, int players, bool won) => won ? Math.Max(0, fee) * Math.Max(0, players - 1) : -Math.Max(0, fee);

        public static bool CanAfford(int coins, int fee) => coins >= fee;

        public static string Label(int fee) => fee <= 0 ? "Free" : fee >= 1000 ? (fee / 1000) + "K" : fee.ToString();
    }

    /// <summary>
    /// The daily reward: a 7-day calendar. Claim once a day; coming back the next day moves one day on, missing a day starts
    /// again at day 1; after day 7 it starts over. Days are calendar days (yyyyMMdd numbers, the phone's local date).
    /// </summary>
    public static class DailyReward
    {
        public static readonly int[] Amounts = { 100, 200, 300, 400, 500, 750, 1500 };

        public static int DayKey(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        public static bool CanClaim(int lastClaimDay, DateTime today) => lastClaimDay != DayKey(today);

        /// <summary>Which calendar day (1..7) today's claim is, given the last claim and the streak then.</summary>
        public static int NextDay(int lastClaimDay, int lastStreak, DateTime today)
        {
            if (lastClaimDay == DayKey(today)) return Math.Max(1, lastStreak);                 // already claimed today: that day
            bool yesterday = lastClaimDay == DayKey(today.AddDays(-1));
            if (!yesterday || lastStreak <= 0) return 1;
            return lastStreak >= Amounts.Length ? 1 : lastStreak + 1;
        }

        public static int AmountFor(int day) => Amounts[Math.Max(1, Math.Min(Amounts.Length, day)) - 1];
    }
}
