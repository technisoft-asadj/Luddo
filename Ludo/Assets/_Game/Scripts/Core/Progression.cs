using System;

namespace Ludo.Core
{
    /// <summary>
    /// Levels, XP and coins, pure and testable. The level curve lives in this one file (what a match pays is in RewardSettings / RewardRules). XP needed for the next level grows a little each level:
    /// level 1 -> 2 costs 100 XP, level 2 -> 3 costs 150 XP, level 3 -> 4 costs 200 XP, and so on.
    /// Coins are only earned by playing; nothing here can be bought with real money.
    /// </summary>
    public static class Progression
    {
        public const int MaxLevel = 100;
        public const int BaseXp = 100;          // XP from level 1 to level 2
        public const int StepXp = 50;           // each further level costs this much more

        /// <summary>XP needed to go from 'level' to the next level.</summary>
        public static int XpToNext(int level) => BaseXp + StepXp * (Math.Max(1, level) - 1);

        /// <summary>Total XP a player must have to BE at 'level' (level 1 = 0).</summary>
        public static long TotalXpFor(int level)
        {
            long n = Math.Max(1, Math.Min(level, MaxLevel)) - 1;
            return n * BaseXp + (long)StepXp * n * (n - 1) / 2;
        }

        public static int LevelFor(long totalXp)
        {
            int level = 1;
            while (level < MaxLevel && totalXp >= TotalXpFor(level + 1)) level++;
            return level;
        }

        /// <summary>XP earned inside the current level.</summary>
        public static int XpIntoLevel(long totalXp)
        {
            int level = LevelFor(totalXp);
            return level >= MaxLevel ? 0 : (int)(totalXp - TotalXpFor(level));
        }

        /// <summary>0..1: how far the player is towards the next level (1 at the top level).</summary>
        public static float Fraction(long totalXp)
        {
            int level = LevelFor(totalXp);
            return level >= MaxLevel ? 1f : (float)XpIntoLevel(totalXp) / XpToNext(level);
        }
    }
}
