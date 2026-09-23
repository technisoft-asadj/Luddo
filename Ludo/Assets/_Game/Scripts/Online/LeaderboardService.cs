using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace Ludo.Online
{
    /// <summary>One line of a leaderboard.</summary>
    public readonly struct BoardRow
    {
        public readonly int Rank;               // 1 = best
        public readonly string PlayerId;
        public readonly string Name;
        public readonly int Score;
        public readonly bool IsMe;
        public readonly string Country;         // ISO code the player attached to their own score ("" = none)
        public BoardRow(int rank, string id, string name, int score, bool isMe, string country = "")
        {
            Rank = rank; PlayerId = id; Name = name; Score = score; IsMe = isMe; Country = Ludo.Core.Countries.Normalize(country);
        }
    }

    /// <summary>
    /// The global leaderboards (Unity Leaderboards): the all-time Ranked rating and the Weekly Cup. The boards themselves
    /// are created once in the Unity Dashboard (see RELEASE_GUIDE.md); until then every call fails politely and
    /// LastError says "not set up yet". Nothing here throws.
    /// </summary>
    public static class LeaderboardService
    {
        public static string RatingBoard => OnlineConfig.Load()?.ratingBoardId ?? "ludovibe_rating";
        public static string WeeklyBoard => OnlineConfig.Load()?.weeklyBoardId ?? "ludovibe_weekly";

        public static string LastError { get; private set; } = "";

        /// <summary>Put my score on a board (best/latest/total is decided by how the board was created).</summary>
        public static async Task<bool> PostAsync(string boardId, double score)
        {
            try
            {
                if (!await OnlineService.ConnectAsync()) return false;
                // the score carries my country (only I can write my own entry); others read it to show my flag
                var meta = new Dictionary<string, string> { { "country", Ludo.Game.GameSettings.Country } };
                await LeaderboardsService.Instance.AddPlayerScoreAsync(boardId, score, new AddPlayerScoreOptions { Metadata = meta });
                return true;
            }
            catch (Exception e)
            {
                LastError = Describe(e);
                Debug.LogWarning("[Ludo] Leaderboard post: " + LastError);
                return false;
            }
        }

        /// <summary>The best players of a board, or null (see LastError).</summary>
        public static async Task<List<BoardRow>> TopAsync(string boardId, int limit = 50)
        {
            try
            {
                if (!await OnlineService.ConnectAsync()) { LastError = OnlineService.LastError; return null; }
                var page = await LeaderboardsService.Instance.GetScoresAsync(boardId, new GetScoresOptions { Limit = limit, IncludeMetadata = true });
                var rows = new List<BoardRow>();
                foreach (var e in page.Results) rows.Add(Convert(e));
                return rows;
            }
            catch (Exception e)
            {
                LastError = Describe(e);
                return null;
            }
        }

        /// <summary>My own place on a board (even when I am far down the list), or null if I have no score yet.</summary>
        public static async Task<BoardRow?> MineAsync(string boardId)
        {
            try
            {
                if (!await OnlineService.ConnectAsync()) return null;
                var e = await LeaderboardsService.Instance.GetPlayerScoreAsync(boardId, new GetPlayerScoreOptions { IncludeMetadata = true });
                return Convert(e);
            }
            catch (Exception e)
            {
                LastError = Describe(e);
                return null;
            }
        }

        static BoardRow Convert(Unity.Services.Leaderboards.Models.LeaderboardEntry e) =>
            new BoardRow(e.Rank + 1, e.PlayerId, OnlineService.ShownName(e.PlayerName), (int)e.Score, e.PlayerId == OnlineService.PlayerId, CountryIn(e.Metadata));

        [Serializable] sealed class ScoreMeta { public string country = ""; }

        /// <summary>The country in a score's metadata ("" when there is none or it is not valid).</summary>
        public static string CountryIn(string metadataJson)
        {
            if (string.IsNullOrEmpty(metadataJson)) return "";
            try { return Ludo.Core.Countries.Normalize(JsonUtility.FromJson<ScoreMeta>(metadataJson)?.country); }
            catch (Exception) { return ""; }
        }

        static string Describe(Exception e)
        {
            string text = (e.Message ?? "").ToLowerInvariant();
            if (text.Contains("not found") || text.Contains("could not be found") || text.Contains("404") || text.Contains("does not exist") || text.Contains("27005"))
                return "The world leaderboard is not switched on yet. Your results are still saved.";
            return OnlineService.Describe(e);
        }
    }
}
