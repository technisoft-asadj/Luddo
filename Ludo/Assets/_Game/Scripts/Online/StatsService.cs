using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using UnityEngine;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>What other players can see about you: your rating and results. Stored in Unity Cloud Save (public data).</summary>
    [Serializable]
    public sealed class PlayerStats
    {
        public string name = "";
        public int avatar;
        public string country = "";             // ISO code the player chose ("" = none); others see it as a flag
        public int rating = Rating.Start;       // the player's Rank Points (the field keeps its old name in the cloud save)
        public int games, wins;                 // every online match
        public int rankedGames, rankedWins;
        public int streak, bestStreak;          // wins in a row
        public string weekId = "";              // the Weekly Cup week the points below belong to
        public int weeklyPoints;
        public int xp;                          // total experience ever earned (the level is worked out from it)
        public int coins;                       // the in-game currency, earned by playing only
        public int disconnects;                 // ranked matches lost because the connection dropped and did not come back in time
        public int schema;                      // 2 = Rank Points (older saves held an Elo rating and are restarted)
        public string recent = "";              // ids of the last matches recorded, so one match can never be counted twice
        public bool starter;                    // the starter coins were given (once per profile)
        public int dailyDay;                    // the day (yyyyMMdd) the daily reward was last claimed
        public int dailyStreak;                 // which day of the 7-day calendar that was (1..7)
        public int chestAt;                     // Chest.Stamp of the last free chest opened (0 = never)
        public string diceSkin = "";            // the dice design in use ("" = the default Classic)
        public string diceOwned = "";           // ids of the designs bought with coins (level/rank ones open by themselves)

        /// <summary>The dice design this player actually rolls with (falls back to Classic if the chosen one is not theirs).</summary>
        public string EquippedDice => DiceSkins.Equipped(string.IsNullOrEmpty(diceSkin) ? DiceSkins.Default : diceSkin, Level, rating, diceOwned);

        public int WinRatePercent => games == 0 ? 0 : Mathf.RoundToInt(100f * wins / games);
        public string Tier => Rating.Tier(rating);
        public int Level => Progression.LevelFor(xp);
        public int Losses => Mathf.Max(0, games - wins);
        public int DisconnectRatePercent => games == 0 ? 0 : Mathf.RoundToInt(100f * disconnects / games);
    }

    /// <summary>The result of one online match for this player: what RewardRules said it is worth.</summary>
    [Serializable]
    public struct MatchOutcome
    {
        public string matchId;
        public MatchMode mode;
        public ResultKind result;
        public int rankChange;                  // signed Rank Points (before the minimum is applied)
        public int xpChange;                    // signed XP
        public int coins;
        public int entryCoins;                  // coin table: signed (winner +the other entries, others -their entry; 0 = free table)

        public bool Ranked => mode != MatchMode.Casual;
        public bool Won => result == ResultKind.Win;

        public static MatchOutcome From(MatchPayout p, string matchId, int entryCoins = 0) => new MatchOutcome
        {
            matchId = matchId ?? "", mode = p.Mode, result = p.Result, rankChange = p.RankPoints, xpChange = p.Xp, coins = p.Coins, entryCoins = entryCoins
        };
    }

    /// <summary>
    /// Online statistics: load and save your own, read other players' (their public stats), and record a finished match
    /// (games, wins, streak, rating, Weekly Cup points) and send the new scores to the leaderboards.
    /// Honest note: the phone writes its own result, so a hacked phone could cheat its stats. A server-side check
    /// (Unity Cloud Code) is the upgrade if the game grows.
    /// </summary>
    public static class StatsService
    {
        const string Key = "stats";

        public static PlayerStats Mine { get; private set; } = new PlayerStats();
        public static bool Loaded { get; private set; }
        public static string LastError { get; private set; } = "";
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Mine = new PlayerStats();
            Loaded = false;
            watching = false;
            photoSeen = 0;
            LastError = "";
            Changed = null;
            flushing = false;
            flushAgain = false;
            bonusApplied = 0;
            appliedIds.Clear();
        }

        /// <summary>Load my stats from the cloud (or start fresh). Also keeps my name and picture up to date for others to see.</summary>
        public static async Task<PlayerStats> LoadMineAsync(bool force = false)
        {
            if (Loaded && !force) return Mine;
            if (await LoadMineAsyncNoFlush() == null) return Mine;
            if (!watching) { watching = true; GameSettings.Changed += OnProfileEdited; }
            Changed?.Invoke();
            await FlushPendingAsync();                  // results that could not be saved before (a dropped connection, a closed app)
            return Mine;
        }

        static bool watching;
        static int syncTicket;

        /// <summary>The player edited their profile (name or picture): after a short pause, tell the cloud so friends see the change.</summary>
        static async void OnProfileEdited()
        {
            if (!OnlineService.IsReady) return;            // logged out: nothing to sync, and syncing must not sign anybody in
            bool photoChanged = photoSeen != ProfilePhoto.Version;
            photoSeen = ProfilePhoto.Version;
            if (photoChanged) _ = PhotoService.SyncMineAsync();                 // my new (or removed) photo goes to the cloud
            if (!Loaded || (Mine.name == GameSettings.PlayerName(0) && Mine.avatar == GameSettings.AvatarIndex(0) && (Mine.country ?? "") == GameSettings.Country)) return;
            int ticket = ++syncTicket;
            await Task.Delay(1500);                    // wait until they stop typing
            if (ticket != syncTicket) return;
            await SaveMineAsync();
            await OnlineService.PushProfileNameAsync();
            Changed?.Invoke();
        }

        static int photoSeen;

        /// <summary>Somebody's public stats, or null if they have none yet or it failed.</summary>
        public static async Task<PlayerStats> LoadOfAsync(string playerId)
        {
            try
            {
                if (!await OnlineService.ConnectAsync()) return null;
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { Key }, new LoadOptions(new PublicReadAccessClassOptions(playerId)));
                return items.TryGetValue(Key, out var item) ? item.Value.GetAs<PlayerStats>() : null;
            }
            catch (Exception e)
            {
                LastError = OnlineService.Describe(e);
                Debug.LogWarning("[Ludo] Stats load: " + LastError);
                return null;
            }
        }

        /// <summary>Save my stats so others can see them.</summary>
        public static async Task<bool> SaveMineAsync()
        {
            try
            {
                if (!await OnlineService.ConnectAsync()) return false;
                Mine.name = GameSettings.PlayerName(0);
                Mine.avatar = GameSettings.AvatarIndex(0);
                Mine.country = GameSettings.Country;
                await CloudSaveService.Instance.Data.Player.SaveAsync(
                    new Dictionary<string, object> { { Key, Mine } }, new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new PublicWriteAccessClassOptions()));
                return true;
            }
            catch (Exception e)
            {
                LastError = OnlineService.Describe(e);
                Debug.LogWarning("[Ludo] Stats save: " + LastError);
                return false;
            }
        }

        /// <summary>Forget the stats held in memory (another player signed in on this phone); they are read again from the cloud when needed.</summary>
        public static void ForgetMine()
        {
            Mine = new PlayerStats();
            Loaded = false;
        }

        /// <summary>Erase my saved stats from the cloud (part of "Delete Online Account"). Best effort.</summary>
        public static async Task DeleteMineAsync()
        {
            try
            {
                await CloudSaveService.Instance.Data.Player.DeleteAsync(
                    Key, new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions(new PublicWriteAccessClassOptions()));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Stats delete: " + e.Message);      // nothing saved yet is fine
            }
            Mine = new PlayerStats();
            Loaded = false;
        }

        /// <summary>
        /// Record a finished match: it is written to the phone's pending list FIRST, then applied and saved to the cloud.
        /// If there is no connection (or the app closes) the result is not lost: it is applied the next time the stats load.
        /// </summary>
        public static async Task RecordAsync(MatchOutcome outcome)
        {
            PendingOutcomes.Set(outcome);
            await FlushPendingAsync();
        }

        static bool flushing;
        static bool flushAgain;
        static int bonusApplied;                            // ad-bonus points already added to Mine but not saved yet
        static readonly HashSet<string> appliedIds = new HashSet<string>();

        /// <summary>Apply every pending result (and ad bonus) to my stats, save them, update the leaderboards.</summary>
        public static async Task FlushPendingAsync()
        {
            if (flushing) { flushAgain = true; return; }
            flushing = true;
            try
            {
                do { flushAgain = false; if (!await FlushOnce()) break; } while (flushAgain);
            }
            finally { flushing = false; }
        }

        /// <summary>One pass. False when it could not finish (no connection): everything stays pending for the next load.</summary>
        static async Task<bool> FlushOnce()
        {
            int bonus = PendingOutcomes.Bonus;
            if (PendingOutcomes.Count == 0 && bonus == 0) return true;
            if (!Loaded && await LoadMineAsyncNoFlush() == null) return false;

            var items = PendingOutcomes.All();
            bool ranked = bonus > 0;
            foreach (var o in items)
            {
                bool known = !string.IsNullOrEmpty(o.matchId) && (appliedIds.Contains(o.matchId) || Mine.recent.Split(',').Contains(o.matchId));
                if (known) continue;                            // this match was already counted
                if (!string.IsNullOrEmpty(o.matchId)) appliedIds.Add(o.matchId);
                Apply(Mine, o, DateTime.UtcNow);
                ranked |= o.Ranked;
            }
            if (bonus > bonusApplied)
            {
                Mine.rating = RewardRules.ApplyRank(Mine.rating, bonus - bonusApplied);
                bonusApplied = bonus;
            }
            Changed?.Invoke();
            if (!await SaveMineAsync()) return false;           // stays pending: tried again at the next load
            foreach (var o in items) PendingOutcomes.Remove(o.matchId);
            if (bonus > 0) { PendingOutcomes.ClearBonus(bonus); bonusApplied = Math.Max(0, bonusApplied - bonus); }
            if (ranked)
            {
                await LeaderboardService.PostAsync(LeaderboardService.RatingBoard, Mine.rating);
                await LeaderboardService.PostAsync(LeaderboardService.WeeklyBoard, Mine.weeklyPoints);
            }
            return true;
        }

        static async Task<PlayerStats> LoadMineAsyncNoFlush()
        {
            if (!await OnlineService.ConnectAsync()) { LastError = OnlineService.LastError; return null; }
            var loaded = await LoadOfAsync(OnlineService.PlayerId);
            if (loaded != null)
            {
                if (loaded.schema < 2) { loaded.rating = Rating.Start; loaded.schema = 2; }      // the old Elo rating is not Rank Points: start over
                Mine = loaded;
            }
            else Mine.schema = 2;
            Mine.name = GameSettings.PlayerName(0);
            Mine.avatar = GameSettings.AvatarIndex(0);
            Mine.country = GameSettings.Country;
            Loaded = true;
            GameSettings.DiceSkin = Mine.EquippedDice;           // signing in on another phone brings the chosen dice along
            if (!Mine.starter)                                   // a new profile: the starter coins, once
            {
                Mine.starter = true;
                Mine.coins += CoinTables.StarterCoins;
                await SaveMineAsync();
            }
            return Mine;
        }

        /// <summary>The rewarded ad doubled the Rank Points of a win: the bonus (Rank Points only) is queued on the phone and saved.</summary>
        public static async Task AddRankBonusAsync(int points)
        {
            if (points <= 0) return;
            PendingOutcomes.AddBonus(points);
            await FlushPendingAsync();
        }

        /// <summary>
        /// Claim today's daily reward ('doubled' = only after a rewarded ad was watched to the end). Returns the coins added,
        /// or 0 when it was already claimed today or the profile could not be loaded.
        /// </summary>
        public static async Task<int> ClaimDailyAsync(bool doubled)
        {
            await LoadMineAsync();
            if (!Loaded) return 0;
            var today = DateTime.Now;
            if (!DailyReward.CanClaim(Mine.dailyDay, today)) return 0;
            int day = DailyReward.NextDay(Mine.dailyDay, Mine.dailyStreak, today);
            int amount = DailyReward.AmountFor(day) * (doubled ? 2 : 1);
            Mine.dailyDay = DailyReward.DayKey(today);
            Mine.dailyStreak = day;
            Mine.coins += amount;
            Changed?.Invoke();
            await SaveMineAsync();
            return amount;
        }

        // ---------- free chest ----------

        /// <summary>Is the free chest ready to open right now?</summary>
        public static bool ChestReady => Loaded && Chest.IsReady(Mine.chestAt, DateTime.UtcNow);

        /// <summary>
        /// Open the free chest. Returns the coins it held (0 = it was not ready). The amount is drawn from the stamp of
        /// this open, so it cannot be rerolled by closing and reopening the screen. 'doubled' is only ever passed after
        /// the ads SDK has confirmed a finished rewarded ad - the ad is a bonus, never a condition.
        /// </summary>
        public static async Task<int> OpenChestAsync(bool doubled)
        {
            await LoadMineAsync();
            if (!Loaded) return 0;
            var now = DateTime.UtcNow;
            if (!Chest.IsReady(Mine.chestAt, now)) return 0;
            int stamp = Chest.Stamp(now);
            int amount = Chest.CoinsFor(stamp) * (doubled ? 2 : 1);
            Mine.chestAt = stamp;
            Mine.coins += amount;
            Changed?.Invoke();
            await SaveMineAsync();
            return amount;
        }

        /// <summary>
        /// A player who is out of coins watched a rewarded ad to the end: give them a small top-up. Only while they are
        /// really low (Chest.BrokeBelow), so it cannot be farmed. Returns the coins added (0 = not needed / not loaded).
        /// </summary>
        public static async Task<int> AddAdCoinsAsync()
        {
            await LoadMineAsync();
            if (!Loaded || !Chest.CanWatchForCoins(Mine.coins)) return 0;
            Mine.coins += Chest.AdCoins;
            Changed?.Invoke();
            await SaveMineAsync();
            return Chest.AdCoins;
        }

        // ---------- dice collection ----------

        /// <summary>
        /// Buy a dice design with coins. Nothing happens - and nothing is charged - unless the design is really for sale,
        /// not already owned and affordable, which Ludo.Core.DiceSkins decides. Returns what it cost (0 = not bought).
        /// The design is purely a picture; see DiceSkins for why it can never change what the dice rolls.
        /// </summary>
        public static async Task<int> BuyDiceAsync(string id)
        {
            await LoadMineAsync();
            if (!Loaded) return 0;
            if (!DiceSkins.CanBuy(id, Mine.coins, Mine.Level, Mine.rating, Mine.diceOwned, out int price)) return 0;
            Mine.coins -= price;
            Mine.diceOwned = DiceSkins.Add(Mine.diceOwned, id);
            Changed?.Invoke();
            await SaveMineAsync();
            return price;
        }

        /// <summary>
        /// Wear a dice design. Only one the player owns is accepted. The choice is saved both in the profile (so another
        /// phone sees it) and on this phone (so the Game scene can read it without the online layer).
        /// </summary>
        public static async Task<bool> EquipDiceAsync(string id)
        {
            await LoadMineAsync();
            if (!Loaded || !DiceSkins.IsOwned(id, Mine.Level, Mine.rating, Mine.diceOwned)) return false;
            Mine.diceSkin = id;
            Ludo.Game.GameSettings.DiceSkin = id;
            Changed?.Invoke();
            await SaveMineAsync();
            return true;
        }

        /// <summary>The bookkeeping of one match (pure, so it can be tested).</summary>
        public static void Apply(PlayerStats s, MatchOutcome o, DateTime utcNow)
        {
            s.games++;
            if (o.Won) { s.wins++; s.streak++; s.bestStreak = Math.Max(s.bestStreak, s.streak); }
            else s.streak = 0;
            if (o.result == ResultKind.DisconnectLoss) s.disconnects++;

            s.xp = (int)RewardRules.ApplyXp(s.xp, o.xpChange);
            s.coins = Math.Max(0, s.coins + Math.Max(0, o.coins) + o.entryCoins);
            if (!string.IsNullOrEmpty(o.matchId))
            {
                var ids = new List<string>(s.recent.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { o.matchId };
                if (ids.Count > 20) ids.RemoveRange(0, ids.Count - 20);
                s.recent = string.Join(",", ids);
            }

            if (!o.Ranked) return;
            s.rankedGames++;
            if (o.Won) s.rankedWins++;
            s.rating = RewardRules.ApplyRank(s.rating, o.rankChange);

            string week = WeeklyCup.WeekId(utcNow);
            if (s.weekId != week) { s.weekId = week; s.weeklyPoints = 0; }       // a new week: the Weekly Cup starts from zero
            if (o.result == ResultKind.Win || o.result == ResultKind.Loss) s.weeklyPoints += WeeklyCup.PointsFor(o.Won);   // leaving a match earns nothing
        }
    }
}
