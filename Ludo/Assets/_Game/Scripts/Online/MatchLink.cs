using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>One seat of an online match, as the host decided it.</summary>
    [Serializable]
    public sealed class MatchSeat
    {
        public string playerId = "";     // the online player who sits here ("" = the computer)
        public string name = "";
        public int avatar;
        public string country = "";      // ISO code from the player's own lobby data ("" = none)
        public int rating = Ludo.Core.Rating.Start;   // the player's ranked rating when the match started
        public int ai = -1;              // -1 = a person, otherwise the AiDifficulty number
    }

    /// <summary>Everything the guests need to start the game: who sits where, and whether it counts for the ranking.</summary>
    [Serializable]
    public sealed class MatchStart
    {
        public MatchSeat[] seats = new MatchSeat[0];
        public bool ranked;                // Quick Match (and tournaments): Rank Points and XP change. A private room is casual.
        public int mode;                   // the game mode (Ludo.Core.GameMode): every phone plays the same rules
        public string matchId = "";        // unique per match
    }

    /// <summary>One line of text chat, as every phone shows it.</summary>
    public sealed class ChatMessage
    {
        public string playerId = "";
        public string name = "";
        public string text = "";
        public bool mine;
    }

    /// <summary>
    /// Carries the game messages between the phones, on top of Netcode for GameObjects' "named messages".
    /// Every message is a short text such as "R|4" (dice showed 4). Lives across scene loads, so the lobby, the game
    /// and the result screen all talk through the same link. Implements IMatchLink for GameController.
    /// </summary>
    public sealed class MatchLink : MonoBehaviour, IMatchLink
    {
        const string MessageName = "ludovibe";

        public static MatchLink Current { get; private set; }

        NetworkManager network;
        bool registered;
        bool helloSent;
        bool connected = true;

        string localPlayerId = "";
        string[] seatOwner = new string[4];                                    // PLAYER number -> player id (host uses it to check senders)
        readonly Dictionary<ulong, string> playerOfClient = new Dictionary<ulong, string>();

        readonly Queue<int> rolls = new Queue<int>();
        readonly Queue<int> picks = new Queue<int>();
        readonly Queue<int> takeovers = new Queue<int>();
        readonly Queue<int> departed = new Queue<int>();
        // reconnecting: the host keeps the whole story of the match and holds a dropped player's seat for a while
        public static float ReconnectSeconds => RewardSettings.Active.reconnectGraceSeconds;      // configurable grace period
        readonly List<int> history = new List<int>();                          // host: 1..6 = dice value, 10+n = pick n
        readonly List<int> takenOver = new List<int>();                        // players the computer plays now
        readonly Dictionary<string, float> away = new Dictionary<string, float>();      // host: player id -> when the seat is given up
        bool matchStarted;
        MatchResync pendingResync;
        bool reconnecting;
        float reconnectUntil;
        bool hostGone;
        readonly Dictionary<int, float> awayView = new Dictionary<int, float>();      // seat -> when it forfeits (every phone shows the countdown)
        readonly List<string> rollRequests = new List<string>();               // player ids that asked to roll
        readonly List<KeyValuePair<string, int>> pickRequests = new List<KeyValuePair<string, int>>();
        MatchStart pendingStart;

        public bool IsHost => network != null && network.IsHost;
        public bool IsConnected => connected && network != null && network.IsListening;

        public event Action<string> PlayerLeft;         // lobby: someone (player id) disconnected

        // text chat: one shared history for the waiting room and the match (this link lives through both)
        const int ChatHistoryLimit = 60;
        readonly List<ChatMessage> chat = new List<ChatMessage>();
        readonly Dictionary<string, ChatThrottle> throttles = new Dictionary<string, ChatThrottle>();
        readonly ChatThrottle ownThrottle = new ChatThrottle();
        public IReadOnlyList<ChatMessage> ChatHistory => chat;
        public event Action<ChatMessage> ChatReceived;

        // ---------- lifecycle ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        /// <summary>Create the link for the running network (after a room has been created or joined).</summary>
        public static MatchLink Open(string localPlayerId)
        {
            Close();
            var go = new GameObject("MatchLink");
            DontDestroyOnLoad(go);
            var link = go.AddComponent<MatchLink>();
            link.localPlayerId = localPlayerId;
            link.network = NetworkManager.Singleton;
            Current = link;
            return link;
        }

        public static void Close()
        {
            if (Current != null) Destroy(Current.gameObject);
            Current = null;
        }

        void Update()
        {
            if (network == null) network = NetworkManager.Singleton;
            if (network == null) return;

            if (registered && network.CustomMessagingManager == null) registered = false;      // the network was restarted (reconnect): register again
            if (!registered && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
                network.OnClientDisconnectCallback -= OnClientDisconnected;
                network.OnClientDisconnectCallback += OnClientDisconnected;
                registered = true;
            }

            // a guest that got its connection back introduces itself again; the host answers with everything it missed
            if (!network.IsHost && !connected && network.IsConnectedClient) { connected = true; helloSent = false; reconnecting = false; }

            // host: a player who did not come back in time is replaced by the computer
            if (network.IsHost && away.Count > 0)
            {
                List<string> expired = null;
                foreach (var kv in away)
                    if (Time.unscaledTime >= kv.Value) (expired ?? (expired = new List<string>())).Add(kv.Key);
                if (expired != null)
                    foreach (var pid in expired)
                    {
                        away.Remove(pid);
                        for (int seat = 0; seat < seatOwner.Length; seat++)
                            if (seatOwner[seat] == pid) { departed.Enqueue(seat); awayView.Remove(seat); }
                    }
            }

            // a guest introduces itself once, so the host can tell whose messages are whose
            if (registered && !helloSent && !network.IsHost && network.IsConnectedClient)
            {
                helloSent = true;
                SendToHost("H|" + localPlayerId);
            }
            if (registered && network.IsHost) playerOfClient[network.LocalClientId] = localPlayerId;
        }

        void OnDestroy()
        {
            if (network != null && network.CustomMessagingManager != null && registered)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
                network.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Current == this) Current = null;
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (network == null) return;
            Debug.Log("[Ludo] Connection closed: client " + clientId + " (host=" + network.IsHost + ") reason='" + network.DisconnectReason + "'");
            if (!network.IsHost)
            {
                connected = false;          // a guest lost its host: the match is over
                return;
            }
            if (playerOfClient.TryGetValue(clientId, out var pid))
            {
                playerOfClient.Remove(clientId);
                if (matchStarted)
                {
                    away[pid] = Time.unscaledTime + ReconnectSeconds;      // hold the seat: the phone may just have lost its signal for a moment
                    for (int seat = 0; seat < seatOwner.Length; seat++)
                        if (seatOwner[seat] == pid) { awayView[seat] = away[pid]; SendToGuests("W|" + seat + "|" + ReconnectSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)); }
                    Debug.Log("[Ludo] " + pid + " lost the connection; seat held for " + ReconnectSeconds + " s");
                }
                else
                {
                    PlayerLeft?.Invoke(pid);
                    for (int seat = 0; seat < seatOwner.Length; seat++)
                        if (seatOwner[seat] == pid) departed.Enqueue(seat);
                }
            }
        }

        // ---------- lobby ----------

        /// <summary>Host: tell every guest the match starts, and remember who owns which seat.</summary>
        public void BroadcastStart(MatchStart start)
        {
            SetSeatOwners(start);
            SendToGuests("S|" + JsonUtility.ToJson(start));
        }

        public void SetSeatOwners(MatchStart start)
        {
            matchStarted = true;
            history.Clear();
            takenOver.Clear();
            away.Clear();
            for (int i = 0; i < seatOwner.Length; i++)
                seatOwner[i] = i < start.seats.Length && start.seats[i].ai < 0 ? start.seats[i].playerId : "";
        }

        /// <summary>Host: has this lobby member's game connection come up (they said hello)? A "start" sent before that would be lost.</summary>
        public bool IsPresent(string playerId)
        {
            if (playerId == localPlayerId) return true;
            foreach (var kv in playerOfClient) if (kv.Value == playerId) return true;
            return false;
        }

        public bool TryTakeStart(out MatchStart start)
        {
            start = pendingStart;
            pendingStart = null;
            return start != null;
        }

        // ---------- text chat ----------

        /// <summary>Send a chat message. Returns false (with the reason) when it was empty or sent too fast.</summary>
        public bool SendChat(string text, out string problem)
        {
            problem = null;
            string clean = ChatRules.Prepare(text);
            if (clean == null) { problem = "Type a message first."; return false; }
            if (!ownThrottle.Allow(Time.unscaledTimeAsDouble)) { problem = "Slow down a little."; return false; }
            if (IsHost) HostAcceptChat(localPlayerId, clean);
            else SendToHost("CQ|" + clean);
            return true;
        }

        /// <summary>Host: check one message, then show it here and pass it to everybody.</summary>
        void HostAcceptChat(string playerId, string rawText)
        {
            string text = ChatRules.Prepare(rawText);                    // never trust what a phone sends: clean it again
            if (text == null || string.IsNullOrEmpty(playerId)) return;
            if (!throttles.TryGetValue(playerId, out var throttle)) throttles[playerId] = throttle = new ChatThrottle();
            if (playerId != localPlayerId && !throttle.Allow(Time.unscaledTimeAsDouble)) return;     // spam from a modified phone is dropped

            string name = "Player";
            foreach (var p in RoomService.Players())
                if (p.Id == playerId) { name = p.Name; break; }
            name = ChatRules.CleanName(name);

            SendToGuests("C|" + playerId + "|" + name + "|" + text);
            AddChat(playerId, name, text);
        }

        void AddChat(string playerId, string name, string text)
        {
            var message = new ChatMessage { playerId = playerId, name = name, text = text, mine = playerId == localPlayerId };
            chat.Add(message);
            if (chat.Count > ChatHistoryLimit) chat.RemoveAt(0);
            if (!message.mine && SafetyService.IsBlocked(playerId)) return;      // blocked people stay silent for me
            ChatReceived?.Invoke(message);
        }

        // ---------- IMatchLink: sending ----------

        public void BroadcastRoll(int value)
        {
            history.Add(value);
            rolls.Enqueue(value);                      // the host hears itself directly
            SendToGuests("R|" + value);
        }

        public void BroadcastPick(int moveIndex)
        {
            history.Add(10 + moveIndex);
            picks.Enqueue(moveIndex);
            SendToGuests("P|" + moveIndex);
        }

        public void BroadcastTakeover(int seat)
        {
            if (!takenOver.Contains(seat)) takenOver.Add(seat);
            takeovers.Enqueue(seat);
            SendToGuests("T|" + seat);
        }

        public void SendRollRequest()
        {
            if (IsHost) rollRequests.Add(localPlayerId);
            else SendToHost("RQ|" + localPlayerId);
        }

        public void SendPickRequest(int moveIndex)
        {
            if (IsHost) pickRequests.Add(new KeyValuePair<string, int>(localPlayerId, moveIndex));
            else SendToHost("PQ|" + localPlayerId + "|" + moveIndex);
        }

        // ---------- IMatchLink: reading ----------

        public bool TryTakeRoll(out int value) => TryDequeue(rolls, out value);
        public bool TryTakePick(out int moveIndex) => TryDequeue(picks, out moveIndex);
        public bool TryTakeTakeover(out int seat) => TryDequeue(takeovers, out seat);
        public bool TryTakeDeparted(out int seat) => TryDequeue(departed, out seat);

        public bool TryTakeRollRequest(int player)
        {
            string owner = OwnerOf(player);
            bool found = owner.Length > 0 && rollRequests.Contains(owner);
            rollRequests.Clear();                      // requests from anybody else are out of turn: drop them
            return found;
        }

        public bool TryTakePickRequest(int player, out int moveIndex)
        {
            moveIndex = -1;
            string owner = OwnerOf(player);
            bool found = false;
            foreach (var r in pickRequests)
                if (owner.Length > 0 && r.Key == owner) { moveIndex = r.Value; found = true; }
            pickRequests.Clear();
            return found;
        }

        public bool IsReconnecting => reconnecting;

        public float AwaySecondsLeft(int player) => awayView.TryGetValue(player, out float until) ? Mathf.Max(0f, until - Time.unscaledTime) : 0f;

        public float ReconnectSecondsLeft => reconnecting ? Mathf.Max(0f, reconnectUntil - Time.unscaledTime) : 0f;

        public bool HostGone => hostGone;

        public bool IsAway(int player)
        {
            string owner = OwnerOf(player);
            return owner.Length > 0 && away.ContainsKey(owner);
        }

        public bool TryTakeResync(out MatchResync resync)
        {
            resync = pendingResync;
            pendingResync = null;
            return resync != null;
        }

        /// <summary>Guest: get back into the room and the network after a drop. Gives up after ReconnectSeconds or when the room is gone.</summary>
        public async void StartReconnect()
        {
            if (IsHost || reconnecting) return;
            reconnecting = true;
            reconnectUntil = Time.unscaledTime + ReconnectSeconds;
            while (this != null && reconnecting && Time.unscaledTime < reconnectUntil)
            {
                if (network != null && network.IsConnectedClient) break;
                var outcome = await RoomService.ReconnectAsync();
                if (this == null) return;
                if (outcome == RoomService.ReconnectOutcome.Gone) { hostGone = true; break; }          // the room no longer exists (the host left): no point trying
                await System.Threading.Tasks.Task.Delay(3000);
            }
            reconnecting = false;
        }

        /// <summary>Host: send one phone the whole story of the match, so its game catches up.</summary>
        void SendResync(ulong clientId)
        {
            var sb = new System.Text.StringBuilder("RS|");
            sb.Append(string.Join(",", takenOver.ConvertAll(x => x.ToString()).ToArray()));
            sb.Append('|');
            sb.Append(string.Join(",", history.ConvertAll(x => x.ToString()).ToArray()));
            Send(sb.ToString(), new List<ulong> { clientId });
        }

        static int[] ParseInts(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return new int[0];
            var parts = csv.Split(',');
            var list = new List<int>(parts.Length);
            foreach (var part in parts)
                if (int.TryParse(part, out int v)) list.Add(v);
            return list.ToArray();
        }

        string OwnerOf(int seat) => seat >= 0 && seat < seatOwner.Length && seatOwner[seat] != null ? seatOwner[seat] : "";

        static bool TryDequeue(Queue<int> q, out int value)
        {
            if (q.Count > 0) { value = q.Dequeue(); return true; }
            value = 0;
            return false;
        }

        // ---------- wire ----------

        void OnMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string text);
            if (string.IsNullOrEmpty(text)) return;
            string[] p = text.Split('|');
            try
            {
                switch (p[0])
                {
                    case "H":
                        if (IsHost && p.Length > 1)
                        {
                            playerOfClient[sender] = p[1];
                            if (matchStarted && away.Remove(p[1]))          // a player who dropped is back: catch them up
                            {
                                for (int seat = 0; seat < seatOwner.Length; seat++)
                                    if (seatOwner[seat] == p[1]) { awayView.Remove(seat); SendToGuests("V|" + seat); }
                                Debug.Log("[Ludo] " + p[1] + " is back; sending the match so far");
                                SendResync(sender);
                            }
                            else if (matchStarted && Array.IndexOf(seatOwner, p[1]) >= 0 && history.Count > 0)
                                SendResync(sender);                       // (came back before the host noticed the drop)
                        }
                        break;
                    case "RS":
                        if (!IsHost && p.Length >= 3)
                        {
                            pendingResync = new MatchResync { takenOver = ParseInts(p[1]), history = ParseInts(p[2]) };
                            rolls.Clear(); picks.Clear(); takeovers.Clear(); awayView.Clear();          // everything sent before this is already inside the story
                        }
                        break;
                    case "RQ":
                        if (IsHost && Sender(sender, p[1])) rollRequests.Add(p[1]);
                        else if (IsHost) Debug.LogWarning("[Ludo] Dropped a roll request from client " + sender + " claiming " + p[1]);
                        break;
                    case "PQ": if (IsHost && Sender(sender, p[1])) pickRequests.Add(new KeyValuePair<string, int>(p[1], int.Parse(p[2]))); break;
                    case "R":  if (!IsHost) rolls.Enqueue(int.Parse(p[1])); break;
                    case "P":  if (!IsHost) picks.Enqueue(int.Parse(p[1])); break;
                    case "T":  if (!IsHost) { int gone = int.Parse(p[1]); awayView.Remove(gone); takeovers.Enqueue(gone); } break;
                    case "W":
                        if (!IsHost && p.Length > 2 && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float secs))
                            awayView[int.Parse(p[1])] = Time.unscaledTime + secs;
                        break;
                    case "V":  if (!IsHost) awayView.Remove(int.Parse(p[1])); break;
                    case "CQ":
                        if (IsHost && playerOfClient.TryGetValue(sender, out var chatter) && text.Length > 3)
                            HostAcceptChat(chatter, text.Substring(3));          // the sender is the connection's owner, not a name in the text
                        break;
                    case "C":
                        if (!IsHost)
                        {
                            var f = text.Split(new[] { '|' }, 4);
                            if (f.Length == 4) AddChat(f[1], ChatRules.CleanName(f[2]), ChatRules.MaskRude(f[3]));
                        }
                        break;
                    case "S":
                        if (!IsHost)
                        {
                            pendingStart = JsonUtility.FromJson<MatchStart>(text.Substring(2));
                            SetSeatOwners(pendingStart);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Ignored a bad match message: " + e.Message);     // never trust the wire
            }
        }

        /// <summary>A request only counts when it comes from the phone that introduced itself as that player.</summary>
        bool Sender(ulong clientId, string playerId) => playerOfClient.TryGetValue(clientId, out var known) && known == playerId;

        void SendToHost(string text)
        {
            if (network == null || !network.IsConnectedClient) return;
            Send(text, new List<ulong> { NetworkManager.ServerClientId });
        }

        void SendToGuests(string text)
        {
            if (network == null || !network.IsHost) return;
            var ids = new List<ulong>();
            foreach (var id in network.ConnectedClientsIds)
                if (id != network.LocalClientId) ids.Add(id);
            if (ids.Count > 0) Send(text, ids);
        }

        void Send(string text, IReadOnlyList<ulong> to)
        {
            if (network.CustomMessagingManager == null) return;
            using (var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(text) + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(text);
                network.CustomMessagingManager.SendNamedMessage(MessageName, to, writer);
            }
        }
    }
}
