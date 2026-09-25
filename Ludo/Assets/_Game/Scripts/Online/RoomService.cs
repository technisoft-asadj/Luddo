using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>One person in the waiting room, as the lobby shows them.</summary>
    public readonly struct RoomPlayer
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Avatar;
        public readonly int Rating;
        public readonly bool IsHost;
        public readonly bool IsMe;
        public readonly bool Ready;       // pressed "Ready" (the host counts as ready by being the host)
        public readonly string PhotoStamp; // "0" = uses an animal avatar, otherwise the stamp of their photo (see PhotoService)
        public readonly int Level;
        public readonly string Country;   // ISO code the player chose ("" = none), written only by that player

        public RoomPlayer(string id, string name, int avatar, int rating, bool isHost, bool isMe, bool ready = false, string photoStamp = "0", int level = 1, string country = "")
        {
            Id = id; Name = name; Avatar = avatar; Rating = rating; IsHost = isHost; IsMe = isMe; Ready = ready || isHost; PhotoStamp = photoStamp; Level = level;
            Country = Countries.Normalize(country);
        }
    }

    /// <summary>
    /// Rooms. Online games are always between real people (2 to 4): a room with a code for friends, Quick Match with
    /// anyone, and Ranked Match with players of a similar rating. A room is a Unity "session" (lobby + Relay connection);
    /// the person who creates it is the host and referees the game. Nothing here throws: every call returns true/false
    /// and leaves the reason in LastError.
    /// </summary>
    public static class RoomService
    {
        const string PropName = "name";
        const string PropAvatar = "avatar";
        const string PropRating = "rating";
        const string PropReady = "ready";
        const string PropPhoto = "photo";
        const string PropLevel = "level";
        const string PropCountry = "country";
        const string PropFee = "fee";                // session property: the coin table's entry fee (0 = free)
        const string PropGameMode = "gmode";          // session property: the game mode ("0".."3", see Ludo.Core.GameMode)
        const string ModePublic = "public";
        const string ModeRanked = "ranked";

        static ISession session;
        static string sessionId = "";

        public enum ReconnectOutcome { Ok, Retry, Gone }

        public static bool InRoom => session != null;
        public static bool IsHost => session != null && session.IsHost;
        public static bool IsPublic { get; private set; }
        public static bool IsRanked { get; private set; }
        public static string Code => session != null ? session.Code : "";
        public static int MaxPlayers => session != null ? session.MaxPlayers : 4;

        /// <summary>The room's game mode (Classic / Master / Arrow / Blitz / Team Up), set by whoever opened it.</summary>
        public static GameMode Mode
        {
            get
            {
                if (session?.Properties != null && session.Properties.TryGetValue(PropGameMode, out var p) && int.TryParse(p.Value, out int m))
                    return ModeFrom(m);
                return GameMode.Classic;
            }
        }

        /// <summary>The room's coin entry fee (Quick Match coin tables; private rooms are free).</summary>
        public static int EntryFee
        {
            get
            {
                if (session?.Properties != null && session.Properties.TryGetValue(PropFee, out var p) && int.TryParse(p.Value, out int f) && CoinTables.IsFee(f))
                    return f;
                return 0;
            }
        }

        public static GameMode ModeFrom(int value) => value >= 0 && value <= (int)GameMode.TeamUp ? (GameMode)value : GameMode.Classic;

        static SessionProperty ModeProperty(GameMode mode) =>
            new SessionProperty(((int)mode).ToString(), VisibilityPropertyOptions.Public, PropertyIndex.String2);

        /// <summary>The table sizes a room can have.</summary>
        public const int MinSize = 2, MaxSize = 4;

        static int ClampSize(int size) => Mathf.Clamp(size, MinSize, MaxSize);
        public static string LastError { get; private set; } = "";

        /// <summary>The room's people or settings changed (someone joined / left / changed picture).</summary>
        public static event Action Changed;
        /// <summary>The room is gone (host left, or we were removed).</summary>
        public static event Action Closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            session = null;
            IsPublic = false;
            IsRanked = false;
            LastError = "";
            Changed = null;
            Closed = null;
        }

        // ---------- entering a room ----------

        public static async Task<bool> CreatePrivateAsync(int size = MaxSize, GameMode mode = GameMode.Classic)
        {
            if (!await Prepare()) return false;
            try
            {
                var options = NewOptions(isPrivate: true, size);
                options.SessionProperties = new Dictionary<string, SessionProperty> { { PropGameMode, ModeProperty(mode) } };
                var hosted = await MultiplayerService.Instance.CreateSessionAsync(options);
                return Entered(hosted, isPublic: false, isRanked: false);
            }
            catch (Exception e) { return Fail(e); }
        }

        public static async Task<bool> JoinByCodeAsync(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) { LastError = "Type the room code first."; return false; }
            if (!await Prepare()) return false;
            try
            {
                var joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, NewJoinOptions());
                return Entered(joined, isPublic: false, isRanked: false);
            }
            catch (Exception e) { return Fail(e, JoinMessage(e)); }
        }

        /// <summary>Quick Match (the ranked mode): join any open public room of this table size that has a free seat, or open one and wait for others.</summary>
        public static async Task<bool> QuickMatchAsync(int size = MaxSize, GameMode mode = GameMode.Classic, int fee = 0)
        {
            size = ClampSize(size);
            if (!await Prepare()) return false;
            try
            {
                var quick = new QuickJoinOptions
                {
                    CreateSession = true,
                    Timeout = TimeSpan.FromSeconds(6),
                    Filters = new List<FilterOption>
                    {
                        new FilterOption(FilterField.StringIndex1, ModePublic, FilterOperation.Equal),
                        new FilterOption(FilterField.NumberIndex2, size.ToString(), FilterOperation.Equal),      // only rooms for the same number of players
                        new FilterOption(FilterField.StringIndex2, ((int)mode).ToString(), FilterOperation.Equal), // ... and the same game mode
                        new FilterOption(FilterField.NumberIndex3, fee.ToString(), FilterOperation.Equal),         // ... and the same coin table
                        new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater)
                    }
                };
                var options = NewOptions(isPrivate: false, size);
                options.SessionProperties = new Dictionary<string, SessionProperty>
                {
                    { "mode", new SessionProperty(ModePublic, VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                    { "size", new SessionProperty(size.ToString(), VisibilityPropertyOptions.Public, PropertyIndex.Number2) },
                    { PropGameMode, ModeProperty(mode) },
                    { PropFee, new SessionProperty(fee.ToString(), VisibilityPropertyOptions.Public, PropertyIndex.Number3) }
                };
                var found = await MultiplayerService.Instance.MatchmakeSessionAsync(quick, options);
                return Entered(found, isPublic: true, isRanked: true);        // Quick Match is the ranked mode
            }
            catch (Exception e) { return Fail(e, "Could not find a match. Try again."); }
        }

        public static async Task LeaveAsync()
        {
            var s = session;
            session = null;
            sessionId = "";
            MatchLink.Close();
            if (s != null)
            {
                Detach(s);
                try
                {
                    if (s.IsHost) await s.AsHost().DeleteAsync();
                    else await s.LeaveAsync();
                }
                catch (Exception e) { Debug.LogWarning("[Ludo] Leaving the room: " + e.Message); }
            }
            OnlineNetwork.Stop();
        }

        // ---------- the room ----------

        public static List<RoomPlayer> Players()
        {
            var list = new List<RoomPlayer>();
            if (session == null) return list;
            foreach (var p in session.Players)
            {
                string name = "Player";
                int avatar = 0, rating = Rating.Start;
                bool ready = false;
                string photo = "0";
                int level = 1;
                string country = "";
                if (p.Properties != null)
                {
                    if (p.Properties.TryGetValue(PropCountry, out var c)) country = c.Value;
                    if (p.Properties.TryGetValue(PropLevel, out var lv)) int.TryParse(lv.Value, out level);
                    if (p.Properties.TryGetValue(PropPhoto, out var ph) && !string.IsNullOrEmpty(ph.Value)) photo = ph.Value;
                    if (p.Properties.TryGetValue(PropName, out var n) && !string.IsNullOrEmpty(n.Value)) name = n.Value;
                    if (p.Properties.TryGetValue(PropAvatar, out var a)) int.TryParse(a.Value, out avatar);
                    if (p.Properties.TryGetValue(PropRating, out var r)) int.TryParse(r.Value, out rating);
                    if (p.Properties.TryGetValue(PropReady, out var rd)) ready = rd.Value == "1";
                }
                list.Add(new RoomPlayer(p.Id, name, avatar, rating, p.Id == session.Host, p.Id == OnlineService.PlayerId, ready, photo, Mathf.Max(1, level), country));
            }
            // host first, then in the order people joined
            list.Sort((x, y) => x.IsHost == y.IsHost ? 0 : (x.IsHost ? -1 : 1));
            return list;
        }

        /// <summary>Everybody the host is waiting for has pressed Ready. Public and ranked rooms start by themselves, so they never wait.</summary>
        public static bool AllReady()
        {
            if (IsPublic) return true;
            foreach (var p in Players())
                if (!p.Ready) return false;
            return true;
        }

        public static bool AmReady()
        {
            foreach (var p in Players())
                if (p.IsMe) return p.Ready;
            return false;
        }

        /// <summary>Tell the room I am (not) ready. The lobby carries the flag to everyone.</summary>
        public static async Task<bool> SetReadyAsync(bool ready)
        {
            if (session == null) return false;
            try
            {
                session.CurrentPlayer.SetProperty(PropReady, new PlayerProperty(ready ? "1" : "0", VisibilityPropertyOptions.Member));
                await session.SaveCurrentPlayerDataAsync();
                Changed?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                LastError = OnlineService.Describe(e);
                Debug.LogWarning("[Ludo] Ready: " + LastError);
                return false;
            }
        }

        /// <summary>Back in the room after a match (rematch): "not ready" again, and my rating is the new one so matchmaking and the ranking stay right.</summary>
        public static async Task RefreshMyPropertiesAsync()
        {
            if (session == null) return;
            try
            {
                session.CurrentPlayer.SetProperty(PropReady, new PlayerProperty("0", VisibilityPropertyOptions.Member));
                session.CurrentPlayer.SetProperty(PropRating, new PlayerProperty(StatsService.Mine.rating.ToString(), VisibilityPropertyOptions.Member));
                session.CurrentPlayer.SetProperty(PropPhoto, new PlayerProperty(PhotoService.MineStamp(), VisibilityPropertyOptions.Member));
                session.CurrentPlayer.SetProperty(PropLevel, new PlayerProperty(StatsService.Mine.Level.ToString(), VisibilityPropertyOptions.Member));
                session.CurrentPlayer.SetProperty(PropCountry, new PlayerProperty(GameSettings.Country, VisibilityPropertyOptions.Member));
                await session.SaveCurrentPlayerDataAsync();
                Changed?.Invoke();
            }
            catch (Exception e) { Debug.LogWarning("[Ludo] Refreshing my room data: " + e.Message); }
        }

        /// <summary>Host: stop new players joining (the game is starting) and hide the room from matchmaking.</summary>
        public static async Task LockAsync()
        {
            if (session == null || !session.IsHost) return;
            try
            {
                var host = session.AsHost();
                host.IsLocked = true;
                await host.SavePropertiesAsync();
            }
            catch (Exception e) { Debug.LogWarning("[Ludo] Locking the room: " + e.Message); }
        }

        /// <summary>Host: the seating plan. Only real people sit at the table (2 to 4); null while there are fewer than 2.</summary>
        public static MatchStart BuildStart(Func<string, bool> present = null)
        {
            var people = Players();
            if (present != null) people = people.FindAll(p => present(p.Id));      // only people whose game connection is up
            if (people.Count < 2) return null;
            var start = new MatchStart { seats = new MatchSeat[Mathf.Min(4, people.Count)], ranked = IsRanked, mode = (int)Mode, matchId = Guid.NewGuid().ToString("N").Substring(0, 12) };
            for (int i = 0; i < start.seats.Length; i++)
                start.seats[i] = new MatchSeat { playerId = people[i].Id, name = people[i].Name, avatar = people[i].Avatar, rating = people[i].Rating, country = people[i].Country, ai = -1 };
            return start;
        }

        // ---------- finding a better room (Quick Match search) ----------

        /// <summary>One open public room another searcher could join.</summary>
        public readonly struct OpenRoom
        {
            public readonly string Id;
            public readonly int Players;
            public OpenRoom(string id, int players) { Id = id; Players = players; }
        }

        /// <summary>The open public rooms with the same mode and table size as my search (not my own, not locked, with a free seat).</summary>
        public static async Task<List<OpenRoom>> FindOpenRoomsAsync(int size, bool ranked, GameMode mode = GameMode.Classic, int fee = 0)
        {
            var found = new List<OpenRoom>();
            try
            {
                var filters = new List<FilterOption>
                {
                    new FilterOption(FilterField.StringIndex1, ranked ? ModeRanked : ModePublic, FilterOperation.Equal),
                    new FilterOption(FilterField.NumberIndex2, ClampSize(size).ToString(), FilterOperation.Equal),
                    new FilterOption(FilterField.StringIndex2, ((int)mode).ToString(), FilterOperation.Equal),
                    new FilterOption(FilterField.NumberIndex3, fee.ToString(), FilterOperation.Equal),
                    new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater)
                };
                if (ranked)
                {
                    int band = Rating.Band(StatsService.Mine.rating);
                    filters.Add(new FilterOption(FilterField.NumberIndex1, Math.Max(0, band - 1).ToString(), FilterOperation.GreaterOrEqual));
                    filters.Add(new FilterOption(FilterField.NumberIndex1, (band + 1).ToString(), FilterOperation.LessOrEqual));
                }
                var results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions { Count = 20, FilterOptions = filters });
                foreach (var info in results.Sessions)
                {
                    if (info.IsLocked || info.Id == sessionId || info.AvailableSlots <= 0) continue;
                    found.Add(new OpenRoom(info.Id, info.MaxPlayers - info.AvailableSlots));
                }
            }
            catch (Exception e) { Debug.LogWarning("[Ludo] Looking for open rooms: " + e.Message); }
            return found;
        }

        /// <summary>Leave my (empty) room and join another open room. False (and I am in no room) when it could not be joined.</summary>
        public static async Task<bool> MergeIntoAsync(string roomId, bool ranked)
        {
            try
            {
                await LeaveAsync();
                OnlineNetwork.Ensure();
                var joined = await MultiplayerService.Instance.JoinSessionByIdAsync(roomId, NewJoinOptions());
                return Entered(joined, isPublic: true, isRanked: true);
            }
            catch (Exception e)
            {
                LastError = OnlineService.Describe(e);
                Debug.LogWarning("[Ludo] Joining the better room: " + LastError);
                return false;
            }
        }

        public static string RoomId => sessionId;

        /// <summary>Can THIS phone reach the online service right now? (a win by "everybody left" only counts if it can)</summary>
        public static async Task<bool> ReachableAsync()
        {
            try
            {
                var check = MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions { Count = 1 });
                var done = await Task.WhenAny(check, Task.Delay(8000));
                if (done != check) return false;
                await check;
                return true;
            }
            catch (Exception) { return false; }
        }

        // ---------- helpers ----------

        static async Task<bool> Prepare()
        {
            LastError = "";
            if (session != null) await LeaveAsync();
            if (!await OnlineService.ConnectAsync()) { LastError = OnlineService.LastError; return false; }
            OnlineNetwork.Ensure();
            _ = StatsService.LoadMineAsync();          // my rating goes into the room so others can see it
            return true;
        }

        static SessionOptions NewOptions(bool isPrivate, int size)
        {
            var options = new SessionOptions
            {
                MaxPlayers = ClampSize(size),
                IsPrivate = isPrivate,
                Name = "ludovibe-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                PlayerProperties = MyProperties()
            };
            return options.WithRelayNetwork();
        }

        static JoinSessionOptions NewJoinOptions() => new JoinSessionOptions { PlayerProperties = MyProperties() };

        static Dictionary<string, PlayerProperty> MyProperties() => new Dictionary<string, PlayerProperty>
        {
            { PropName, new PlayerProperty(GameSettings.PlayerName(0), VisibilityPropertyOptions.Member) },
            { PropAvatar, new PlayerProperty(GameSettings.AvatarIndex(0).ToString(), VisibilityPropertyOptions.Member) },
            { PropRating, new PlayerProperty(StatsService.Mine.rating.ToString(), VisibilityPropertyOptions.Member) },
            { PropPhoto, new PlayerProperty(PhotoService.MineStamp(), VisibilityPropertyOptions.Member) },
            { PropLevel, new PlayerProperty(StatsService.Mine.Level.ToString(), VisibilityPropertyOptions.Member) },
            { PropCountry, new PlayerProperty(GameSettings.Country, VisibilityPropertyOptions.Member) }
        };

        /// <summary>Guest whose connection dropped: rejoin the room I am still a member of. The network client is started again by the session.</summary>
        public static async Task<ReconnectOutcome> ReconnectAsync()
        {
            if (string.IsNullOrEmpty(sessionId)) return ReconnectOutcome.Gone;
            try
            {
                var old = session;
                var back = await MultiplayerService.Instance.ReconnectToSessionAsync(sessionId);
                if (old != null) Detach(old);
                session = back;
                Attach(back);
                Changed?.Invoke();
                return ReconnectOutcome.Ok;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Reconnect: " + e.Message);
                string text = (e.Message ?? "").ToLowerInvariant();
                return text.Contains("deleted") || text.Contains("not found") || text.Contains("404") ? ReconnectOutcome.Gone : ReconnectOutcome.Retry;
            }
        }

        static void Attach(ISession s)
        {
            s.Changed += OnChanged;
            s.PlayerJoined += OnPlayerEvent;
            s.PlayerHasLeft += OnPlayerEvent;
            s.PlayerPropertiesChanged += OnChanged;
            s.Deleted += OnDeleted;
            s.RemovedFromSession += OnDeleted;
        }

        static void Detach(ISession s)
        {
            s.Changed -= OnChanged;
            s.PlayerJoined -= OnPlayerEvent;
            s.PlayerHasLeft -= OnPlayerEvent;
            s.PlayerPropertiesChanged -= OnChanged;
            s.Deleted -= OnDeleted;
            s.RemovedFromSession -= OnDeleted;
        }

        static bool Entered(ISession s, bool isPublic, bool isRanked)
        {
            session = s;
            sessionId = s.Id;
            IsPublic = isPublic;
            IsRanked = isRanked;
            Attach(s);
            MatchLink.Open(OnlineService.PlayerId);
            Changed?.Invoke();
            return true;
        }

        static void OnChanged() => Changed?.Invoke();
        static void OnPlayerEvent(string playerId) => Changed?.Invoke();

        static void OnDeleted()
        {
            session = null;
            Closed?.Invoke();
        }

        /// <summary>A plain sentence for why joining a room by code failed (not found, full, already started, closed, too many tries).</summary>
        public static string JoinMessage(Exception e)
        {
            string text = ((e.Message ?? "") + " " + (e.InnerException != null ? e.InnerException.Message : "")).ToLowerInvariant();
            if (text.Contains("invalid character") || text.Contains("invalid code") || text.Contains("code") && text.Contains("invalid")) return "That is not a valid room code. Check it and try again.";
            if (text.Contains("full")) return "That room is full.";
            if (text.Contains("locked") || text.Contains("started") || text.Contains("in progress")) return "That match has already started.";
            if (text.Contains("delet") || text.Contains("closed")) return "That room was closed.";
            if (text.Contains("429") || text.Contains("too many") || text.Contains("rate")) return "Too many tries. Wait a few seconds and try again.";
            if (text.Contains("transport") || text.Contains("network") || text.Contains("connection")) return "No connection. Check your internet and try again.";
            if (text.Contains("not found") || text.Contains("notfound") || text.Contains("404")) return "Could not find that room. Check the code and try again.";
            return "Could not join that room. Check the code and try again.";
        }

        static bool Fail(Exception e, string friendly = null)
        {
            LastError = friendly ?? OnlineService.Describe(e);
            Debug.LogWarning("[Ludo] Room: " + OnlineService.Describe(e));
            return false;
        }
    }
}
