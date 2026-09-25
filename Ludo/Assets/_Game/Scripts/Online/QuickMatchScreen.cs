using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>One line of the "Match found" list: picture, name, level and rating.</summary>
    [System.Serializable]
    public sealed class FoundRow
    {
        public GameObject root;
        public Image avatar;
        public Image flag;                // the player's country (hidden if none)
        public TMP_Text nameText;
        public TMP_Text infoText;
    }

    /// <summary>
    /// Worldwide Quick Match (and Ranked Match). The player enters the global search: real players who are searching for the
    /// same kind of table (same mode, same table size) are put in one room. The screen shows the search time, how many
    /// players were found and a Cancel button; when the table is ready it shows the "Match found" list for a moment and starts
    /// the game. No computer players are ever added: if not enough people are found the search says so.
    /// The rules (when to start, when to give up, when to move to a better room) live in Ludo.Core.MatchmakingRules.
    /// </summary>
    public sealed class QuickMatchScreen : MonoBehaviour
    {
        /// <summary>Set by the Play Online menu before this screen opens.</summary>
        public static bool PendingRanked;
        public static int PendingSize = 2;
        public static GameMode PendingMode = GameMode.Classic;
        public static int PendingFee;

        [SerializeField] ScreenRouter router;
        [SerializeField] TMP_Text titleText;               // QUICK MATCH / RANKED MATCH
        [SerializeField] GameObject searchPanel;
        [SerializeField] TMP_Text statusText;              // "Searching for players..."
        [SerializeField] TMP_Text timerText;               // 00:18
        [SerializeField] TMP_Text foundText;               // 2 / 4
        [SerializeField] Image[] dots;                     // one per seat
        [SerializeField] Image progressBar;                // fills as players are found
        [SerializeField] TMP_Text[] chips;                 // what is being searched for: rules, table size, entry
        [SerializeField] Color dotOn = new Color(0.3f, 1f, 0.45f);
        [SerializeField] Color dotOff = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField] Button cancelButton;
        [SerializeField] Button retryButton;               // after "not enough players" or an error
        [SerializeField] GameObject foundPanel;
        [SerializeField] TMP_Text foundCountdown;
        [SerializeField] FoundRow[] foundRows;

        enum Phase { Connecting, Searching, Found, Failed }

        Phase phase;
        float elapsed;                     // seconds since the search began (survives a room change)
        bool ranked;
        int size = 2;
        GameMode mode = GameMode.Classic;
        int fee;
        bool starting;                     // the match is loading: leave the room alone
        int ticket;                        // changes on every (re)start so an old search cannot act
        bool mergeBusy;
        float nextMerge;
        float foundLeft;
        MatchStart pendingStart;
        MatchmakingSettings cfg;

        void OnEnable()
        {
            cfg = MatchmakingConfig.Current;
            ranked = PendingRanked;
            size = Mathf.Clamp(PendingSize, RoomService.MinSize, RoomService.MaxSize);
            mode = PendingMode;
            fee = CoinTables.IsFee(PendingFee) ? PendingFee : 0;
            titleText.text = ranked ? "Ranked Match" : "Quick Match";
            if (chips != null && chips.Length >= 3)
            {
                chips[0].text = GameSession.ModeName(mode);
                chips[1].text = size + " players";
                chips[2].text = "Entry " + CoinTables.Label(fee);
            }
            starting = false;
            elapsed = 0f;
            RoomService.Changed += Refresh;
            PhotoService.Loaded += OnPhotoLoaded;
            BeginSearch();
        }

        void OnDisable()
        {
            RoomService.Changed -= Refresh;
            PhotoService.Loaded -= OnPhotoLoaded;
            ticket++;                                          // stops any search still in flight
            if (starting) return;                              // the match takes over the room
            if (RoomService.InRoom) _ = RoomService.LeaveAsync();
        }

        void OnPhotoLoaded(string id) => Refresh();

        // ---------- the search ----------

        async void BeginSearch()
        {
            int mine = ++ticket;
            phase = Phase.Connecting;
            SetPanels(searching: true);
            retryButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(true);
            statusText.text = "Connecting...";                 // (a new search must not keep showing the last one's message)
            foundText.text = "0 / " + size;
            SetDots(0, size);
            mergeBusy = false;
            nextMerge = Time.unscaledTime + cfg.mergePoll;
            bool ok = await RoomService.QuickMatchAsync(size, mode, fee);
            if (this == null) return;
            if (mine != ticket)                                // cancelled while connecting
            {
                if (ok) _ = RoomService.LeaveAsync();
                return;
            }
            if (!ok) { Fail(RoomService.LastError); return; }
            phase = Phase.Searching;
            Refresh();
        }

        void Update()
        {
            if (phase == Phase.Found) { UpdateFound(); return; }
            if (phase != Phase.Searching && phase != Phase.Connecting) return;

            elapsed += Time.unscaledDeltaTime;
            RefreshTimer();
            if (phase != Phase.Searching || mergeBusy) return;

            if (MatchmakingRules.TimedOut(elapsed, cfg))
            {
                if (RoomService.InRoom) _ = RoomService.LeaveAsync();
                Fail("Not enough players found. Try again?");
                return;
            }
            if (!RoomService.InRoom)                           // the host of my room left: search again, the clock keeps running
            {
                BeginSearch();
                return;
            }

            var link = MatchLink.Current;
            if (link != null && link.TryTakeStart(out var start)) { EnterFound(start); return; }

            if (!RoomService.IsHost || link == null) return;
            int players = RoomService.Players().Count;
            // only people whose game connection is up can be seated: a "start" sent to someone still connecting would be lost
            int present = RoomService.Players().FindAll(p => link.IsPresent(p.Id)).Count;
            if (MatchmakingRules.ShouldStart(present, RoomService.MaxPlayers, ranked, elapsed, cfg))
            {
                var plan = RoomService.BuildStart(link.IsPresent);
                if (plan != null)
                {
                    _ = RoomService.LockAsync();
                    link.BroadcastStart(plan);
                    EnterFound(plan);
                    return;
                }
            }
            if (players == 1 && Time.unscaledTime >= nextMerge) TryMerge();
        }

        /// <summary>A host who is alone looks for a room that already has people (or a smaller id) and moves there.</summary>
        async void TryMerge()
        {
            mergeBusy = true;
            int mine = ticket;
            var rooms = await RoomService.FindOpenRoomsAsync(size, ranked, mode, fee);
            if (this == null || mine != ticket) return;
            string best = null; int bestPlayers = 0;
            foreach (var r in rooms)
            {
                if (!MatchmakingRules.ShouldMerge(1, RoomService.RoomId, r.Players, r.Id)) continue;
                if (best == null || r.Players > bestPlayers || (r.Players == bestPlayers && string.CompareOrdinal(r.Id, best) < 0)) { best = r.Id; bestPlayers = r.Players; }
            }
            nextMerge = Time.unscaledTime + cfg.mergePoll;
            if (best == null || !RoomService.IsHost || RoomService.Players().Count != 1) { mergeBusy = false; return; }

            bool ok = await RoomService.MergeIntoAsync(best, ranked);
            if (this == null || mine != ticket) { if (ok) _ = RoomService.LeaveAsync(); return; }
            mergeBusy = false;
            if (!ok) BeginSearch();                            // that room filled up first: search again (the clock keeps running)
            else Refresh();
        }

        void Fail(string message)
        {
            phase = Phase.Failed;
            statusText.text = message;
            timerText.text = FormatTime(elapsed);
            retryButton.gameObject.SetActive(true);
            cancelButton.gameObject.SetActive(true);
            SetDots(0);
            foundText.text = "0 / " + size;
        }

        // ---------- match found ----------

        void EnterFound(MatchStart start)
        {
            phase = Phase.Found;
            pendingStart = start;
            foundLeft = cfg.matchFoundSeconds;
            SetPanels(searching: false);
            cancelButton.gameObject.SetActive(false);              // the table is set: the game starts in a moment
            var people = RoomService.Players();
            for (int i = 0; i < foundRows.Length; i++)
            {
                var row = foundRows[i];
                bool used = i < start.seats.Length;
                row.root.SetActive(used);
                if (!used) continue;
                var seat = start.seats[i];
                int level = 1;
                foreach (var p in people) if (p.Id == seat.playerId) level = p.Level;
                row.nameText.text = seat.name;
                row.infoText.text = "Level " + level + "  -  " + Rating.Tier(seat.rating) + " " + seat.rating;
                row.avatar.sprite = (seat.playerId == OnlineService.PlayerId ? ProfilePhoto.Mine : ProfilePhoto.ForPlayer(seat.playerId)) ?? AvatarLibrary.Get(seat.avatar);
                FlagLibrary.Apply(row.flag, MatchStarter.CountryOf(seat));
            }
            UpdateFound();
        }

        void UpdateFound()
        {
            foundLeft -= Time.unscaledDeltaTime;
            foundCountdown.text = "Starting in " + Mathf.Max(1, Mathf.CeilToInt(foundLeft));
            if (foundLeft > 0f || starting) return;
            starting = true;
            MatchStarter.Begin(pendingStart);
        }

        // ---------- what is shown ----------

        void SetPanels(bool searching)
        {
            searchPanel.SetActive(searching);
            foundPanel.SetActive(!searching);
        }

        void Refresh()
        {
            if (this == null || phase == Phase.Found) return;
            int found = RoomService.InRoom ? RoomService.Players().Count : 0;
            if (RoomService.InRoom)
                foreach (var p in RoomService.Players()) PhotoService.Ensure(p.Id, p.PhotoStamp);
            int target = RoomService.InRoom ? RoomService.MaxPlayers : size;
            if (phase == Phase.Searching || phase == Phase.Connecting)
            {
                foundText.text = found + " / " + target;
                statusText.text = phase == Phase.Connecting ? "Connecting..." : MatchmakingRules.Phase(elapsed, cfg);
                SetDots(found, target);
            }
        }

        void RefreshTimer()
        {
            timerText.text = FormatTime(elapsed);
            if (phase == Phase.Searching) statusText.text = MatchmakingRules.Phase(elapsed, cfg);
        }

        void SetDots(int found, int target = 0)
        {
            if (target <= 0) target = size;
            if (progressBar != null) progressBar.fillAmount = Mathf.Clamp01(found / (float)target);
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].gameObject.SetActive(i < target);
                dots[i].color = i < found ? dotOn : dotOff;
            }
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        // ---------- buttons ----------

        public void Cancel() => router.Back();

        public void Retry()
        {
            elapsed = 0f;
            BeginSearch();
        }
    }
}
