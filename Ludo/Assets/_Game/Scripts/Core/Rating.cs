using System;

namespace Ludo.Core
{
    /// <summary>
    /// Rank Points and ranks, pure and testable. Rank Points are the competitive score (XP and level are separate, see
    /// Progression). The thresholds of every rank and the minimum live in RewardSettings, so nothing here is hard-coded.
    /// How many points a match gives or takes is decided by RewardRules.
    /// </summary>
    public static class Rating
    {
        static RewardSettings S => RewardSettings.Active;

        public static int Start => S.startRankPoints;
        public static int Floor => S.minRankPoints;

        /// <summary>Matchmaking group: players with similar Rank Points share a band.</summary>
        public static int Band(int points) => Math.Max(0, points) / Math.Max(1, S.matchmakingBandWidth);

        /// <summary>Index into RewardSettings.tiers of the rank a player with these points holds (the highest whose minimum is reached).</summary>
        public static int TierIndex(int points)
        {
            var tiers = S.tiers;
            int best = 0;
            for (int i = 0; i < tiers.Length; i++)
                if (points >= tiers[i].minPoints && tiers[i].minPoints >= tiers[best].minPoints) best = i;
            return best;
        }

        public static string Tier(int points) => S.tiers.Length == 0 ? "" : S.tiers[TierIndex(points)].name;

        /// <summary>The next rank up: its name and the Rank Points it needs. False at the top rank.</summary>
        public static bool NextTier(int points, out string name, out int required)
        {
            name = ""; required = 0;
            bool found = false;
            foreach (var t in S.tiers)
                if (t.minPoints > points && (!found || t.minPoints < required)) { name = t.name; required = t.minPoints; found = true; }
            return found;
        }

        /// <summary>0..1: how far the player is between their rank and the next one (1 at the top rank).</summary>
        public static float TierFraction(int points)
        {
            if (!NextTier(points, out _, out int next)) return 1f;
            int from = S.tiers[TierIndex(points)].minPoints;
            return next <= from ? 1f : Math.Max(0f, Math.Min(1f, (float)(points - from) / (next - from)));
        }
    }

    /// <summary>The weekly tournament ("Weekly Cup"): every ranked match earns points, the board resets every Monday (UTC).</summary>
    public static class WeeklyCup
    {
        public const int WinPoints = 3;
        public const int PlayPoints = 1;

        public static int PointsFor(bool won) => won ? WinPoints : PlayPoints;

        /// <summary>The Monday 00:00 (UTC) that started the week containing 'utcNow'.</summary>
        public static DateTime WeekStart(DateTime utcNow)
        {
            int daysSinceMonday = ((int)utcNow.DayOfWeek + 6) % 7;
            return utcNow.Date.AddDays(-daysSinceMonday);
        }

        public static TimeSpan TimeLeft(DateTime utcNow) => WeekStart(utcNow).AddDays(7) - utcNow;

        /// <summary>A short id for the week, e.g. "2026-W39", so a stored value can tell whether it belongs to this week.</summary>
        public static string WeekId(DateTime utcNow)
        {
            var start = WeekStart(utcNow);
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            int week = cal.GetWeekOfYear(start.AddDays(3), System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return start.AddDays(3).Year + "-W" + week.ToString("00");
        }
    }
}
