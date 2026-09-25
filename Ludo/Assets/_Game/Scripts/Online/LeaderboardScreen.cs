using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Leaderboards, three tabs:
    ///   Ranked      - the best ranked ratings in the world
    ///   Weekly Cup  - the weekly tournament: ranked matches earn points (win 3, play 1), the board resets every Monday
    ///   Friends     - you and your friends, by rating
    /// The two world boards live in the Unity Dashboard (see RELEASE_GUIDE.md); until they exist the tab says so.
    /// </summary>
    public sealed class LeaderboardScreen : MonoBehaviour
    {
        /// <summary>Which tab to open next time (set by the button that opens this screen).</summary>
        public static int PendingTab;

        [SerializeField] Button[] tabButtons;             // Ranked, Weekly Cup, Friends
        [SerializeField] Image[] tabFaces;
        [SerializeField] RectTransform listRoot;
        [SerializeField] LeaderboardRowView rowTemplate;
        [SerializeField] TMP_Text infoText;               // the Weekly Cup countdown and rules, or a message
        [SerializeField] TMP_Text emptyText;
        [SerializeField] TMP_Text myLine;                 // "You: #12 - 1234"
        [SerializeField] Color tabOn = new Color(1f, 0.82f, 0.15f);
        [SerializeField] Color tabOff = new Color(0.90f, 0.94f, 1f);
        [SerializeField] Color meColor = new Color(1f, 0.93f, 0.6f);
        [SerializeField] Color normalColor = new Color(0.94f, 0.97f, 1f);

        readonly List<GameObject> spawned = new List<GameObject>();
        int tab;
        int loadTicket;

        void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => Show(index));
            }
        }

        void OnEnable()
        {
            rowTemplate.gameObject.SetActive(false);
            // Select Mode's Tournaments row asks for the Weekly Cup tab; that screen cannot reach this class directly
            if (Ludo.Game.MenuFlow.WeeklyCupTab >= 0) { PendingTab = Ludo.Game.MenuFlow.WeeklyCupTab; Ludo.Game.MenuFlow.WeeklyCupTab = -1; }
            Show(Mathf.Clamp(PendingTab, 0, 2));
        }

        void Update()
        {
            if (tab == 1 && infoText != null && string.IsNullOrEmpty(lastMessage)) infoText.text = CupText();
        }

        string lastMessage = "";

        public async void Show(int index)
        {
            tab = index;
            int ticket = ++loadTicket;
            for (int i = 0; i < tabFaces.Length; i++) tabFaces[i].color = i == tab ? tabOn : tabOff;
            Clear();
            lastMessage = "Loading...";
            infoText.text = lastMessage;
            emptyText.gameObject.SetActive(false);
            myLine.text = "";

            List<BoardRow> rows = null;
            string problem = null;
            try
            {
                if (tab == 2) rows = await FriendRows();
                else
                {
                    string board = tab == 0 ? LeaderboardService.RatingBoard : LeaderboardService.WeeklyBoard;
                    rows = await LeaderboardService.TopAsync(board, 50);
                    if (rows == null) problem = LeaderboardService.LastError;
                    else
                    {
                        var mine = await LeaderboardService.MineAsync(board);
                        if (mine.HasValue) myLine.text = "You:  #" + mine.Value.Rank + "  -  " + mine.Value.Score + (tab == 1 ? " points" : "");
                    }
                }
            }
            catch (Exception e) { problem = OnlineService.Describe(e); }

            if (this == null || ticket != loadTicket) return;           // the player switched tab meanwhile
            lastMessage = "";
            infoText.text = tab == 1 ? CupText() : tab == 0 ? "The best ranked players in the world" : "You and your friends";
            if (problem != null)
            {
                emptyText.text = problem;
                emptyText.gameObject.SetActive(true);
                var mine = StatsService.Mine;                                   // my own saved numbers are always available
                myLine.text = tab == 1 ? "Your points this week:  " + mine.weeklyPoints : "Your Rank Points:  " + mine.rating + "  (" + mine.Tier + ")";
                return;
            }
            if (rows == null || rows.Count == 0)
            {
                emptyText.text = tab == 2 ? "Add friends to compare ratings." : "No scores yet. Play a ranked match!";
                emptyText.gameObject.SetActive(true);
                return;
            }
            foreach (var r in rows) AddRow(r);
        }

        /// <summary>You and your friends, sorted by rating (read from each friend's public stats).</summary>
        async Task<List<BoardRow>> FriendRows()
        {
            var social = await SocialService.StartAsync();
            var list = new List<(string id, PlayerStats stats)>();
            var mine = await StatsService.LoadMineAsync();
            list.Add((OnlineService.PlayerId, mine));
            if (social)
            {
                var friends = SocialService.Friends();
                var loads = friends.Select(f => StatsService.LoadOfAsync(f.Id)).ToArray();
                var results = await Task.WhenAll(loads);
                for (int i = 0; i < friends.Count; i++)
                    if (results[i] != null) list.Add((friends[i].Id, results[i]));
            }
            var sorted = list.OrderByDescending(x => x.stats.rating).ToList();
            var rows = new List<BoardRow>();
            for (int i = 0; i < sorted.Count; i++)
                rows.Add(new BoardRow(i + 1, sorted[i].id, sorted[i].stats.name, sorted[i].stats.rating, sorted[i].id == OnlineService.PlayerId, sorted[i].stats.country));
            var me = rows.FindIndex(r => r.IsMe);
            if (me >= 0) myLine.text = "You:  #" + rows[me].Rank + "  -  " + rows[me].Score;
            return rows;
        }

        string CupText()
        {
            var left = WeeklyCup.TimeLeft(DateTime.UtcNow);
            string time = left.Days > 0 ? left.Days + "d " + left.Hours + "h" : left.Hours + "h " + left.Minutes + "m";
            return "WEEKLY CUP  -  ends in " + time + "\nRanked match: win +" + WeeklyCup.WinPoints + ", play +" + WeeklyCup.PlayPoints;
        }

        void Clear()
        {
            foreach (var o in spawned) if (o != null) Destroy(o);
            spawned.Clear();
        }

        void AddRow(BoardRow r)
        {
            var row = Instantiate(rowTemplate, listRoot);
            row.gameObject.SetActive(true);
            row.rankText.text = r.Rank <= 3 ? new[] { "1st", "2nd", "3rd" }[r.Rank - 1] : "#" + r.Rank;
            row.rankText.color = r.Rank == 1 ? new Color(0.95f, 0.66f, 0.05f) : r.Rank == 2 ? new Color(0.50f, 0.56f, 0.66f) : r.Rank == 3 ? new Color(0.80f, 0.47f, 0.20f) : new Color(0.08f, 0.18f, 0.45f);
            row.nameText.text = r.Name;
            row.scoreText.text = r.Score.ToString();
            row.background.color = r.IsMe ? meColor : normalColor;
            row.avatar.gameObject.SetActive(false);
            FlagLibrary.Apply(row.flag, r.IsMe ? GameSettings.Country : r.Country);
            spawned.Add(row.gameObject);
        }
    }
}
