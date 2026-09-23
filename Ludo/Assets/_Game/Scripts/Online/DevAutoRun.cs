#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Test helper, compiled ONLY into the Editor and Development builds (never a release). A test build started with
    ///   LudoFight.exe -joincode ABC123 -autoplay          join a room by code and play by itself
    ///   LudoFight.exe -quickmatch -autoplay               use Quick Match
    ///   ... -voice                                       also switch voice chat on
    ///   ... -acceptfriends                               accept every friend request that arrives (and log invites)
    ///   ... -ready                                       press Ready in every waiting room
    ///   ... -chat "text"                                 send one chat message in the room and one in the match (all chat is logged)
    ///   ... -rematch                                     press Rematch on every result screen
    ///   ... -dropafter N                                 once a match runs, cut this phone's connection after N seconds (tests reconnecting)
    ///   ... -profile NAME                                sign in as a separate online player (several clients on one computer)
    ///   ... -name TEXT                                   this client's display name
    ///   ... -mode N                                      game mode for -create / -quickmatch (0 Classic, 1 Master, 2 Arrow, 3 Blitz)
    ///   ... -country CODE                                this client's country (ISO code, e.g. PK) - flags test
    ///   ... -size N                                      table size (2-4) for -quickmatch / -ranked
    ///   ... -status                                      log the Quick Match search every second (players found)
    ///   ... -create                                      open a private room (table size from -size) and log its code
    ///   ... -startat N                                   as host of that room, press Start once N people are in and ready
    /// Used to test online play on one computer: the Editor is one player, this build is the other.
    /// </summary>
    public sealed class DevAutoRun : MonoBehaviour
    {
        string code;
        bool quickMatch;
        bool ranked;
        bool voice;
        bool acceptFriends;
        bool autoReady;
        bool rematch;
        string chatText;
        float dropAfter;
        int size = 4;
        bool statusLog = true;
        bool create;
        int startAt;
        Ludo.Core.GameMode mode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string join = null, chat = null;
            float drop = 0f;
            int tableSize = 4;
            bool createRoom = false;
            int startAtCount = 0;
            int gameMode = 0;
            bool quick = false, voice = false, accept = false, ranked = false, ready = false, again = false;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-joincode" && i + 1 < args.Length) join = args[i + 1];
                if (args[i] == "-quickmatch") quick = true;
                if (args[i] == "-ranked") ranked = true;
                if (args[i] == "-voice") voice = true;
                if (args[i] == "-acceptfriends") accept = true;
                if (args[i] == "-ready") ready = true;
                if (args[i] == "-rematch") again = true;
                if (args[i] == "-chat" && i + 1 < args.Length) chat = args[i + 1];
                if (args[i] == "-dropafter" && i + 1 < args.Length) float.TryParse(args[i + 1], out drop);
                if (args[i] == "-profile" && i + 1 < args.Length) OnlineService.DevProfile = args[i + 1];
                if (args[i] == "-name" && i + 1 < args.Length) GameSettings.DevName = args[i + 1];
                if (args[i] == "-country" && i + 1 < args.Length) GameSettings.DevCountry = args[i + 1];
                if (args[i] == "-size" && i + 1 < args.Length) int.TryParse(args[i + 1], out tableSize);
                if (args[i] == "-create") createRoom = true;
                if (args[i] == "-startat" && i + 1 < args.Length) int.TryParse(args[i + 1], out startAtCount);
                if (args[i] == "-mode" && i + 1 < args.Length) int.TryParse(args[i + 1], out gameMode);
            }
            if (join == null && !quick && !ranked && !accept && !createRoom) return;
            OnlineService.ChooseGuest();                       // the test player taps "Continue as Guest" (online needs a login choice)
            var go = new GameObject("DevAutoRun");
            DontDestroyOnLoad(go);
            var run = go.AddComponent<DevAutoRun>();
            run.code = join;
            run.quickMatch = quick;
            run.ranked = ranked;
            run.voice = voice;
            run.acceptFriends = accept;
            run.autoReady = ready;
            run.rematch = again;
            run.chatText = chat;
            run.dropAfter = drop;
            run.size = tableSize;
            run.create = createRoom;
            run.startAt = startAtCount;
            run.mode = RoomService.ModeFrom(gameMode);
        }

        IEnumerator Start()
        {
            Debug.Log("[Ludo] DevAutoRun: started (join=" + code + " quick=" + quickMatch + " voice=" + voice + " friends=" + acceptFriends + ")");
            ScreenRouter router = null;
            while (router == null || router.Current < 1)
            {
                router = FindFirstObjectByType<ScreenRouter>();
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            router.Show(8);                                    // Play Online (connects to the services)

            if (acceptFriends)
            {
                var social = SocialService.StartAsync();
                while (!social.IsCompleted) yield return null;
                Debug.Log("[Ludo] DevAutoRun: friends ready=" + social.Result + " myId=" + SocialService.MyFriendId);
                SocialService.Changed += AcceptAll;
                SocialService.InviteReceived += (invite, from) => Debug.Log("[Ludo] DevAutoRun: INVITE received, room " + invite.code + " from " + invite.fromName);
            }

            if (code == null && !quickMatch && !ranked && !create) yield break;

            if (ranked || quickMatch)                          // the worldwide search screen does the rest by itself
            {
                QuickMatchScreen.PendingRanked = ranked;
                QuickMatchScreen.PendingSize = size;
                QuickMatchScreen.PendingMode = mode;
                router.Show(15);
                StartCoroutine(Housekeeping());
                float since = Time.realtimeSinceStartup;
                int lastFound = -1;
                while (true)
                {
                    yield return new WaitForSeconds(1f);
                    int found = RoomService.InRoom ? RoomService.Players().Count : 0;
                    if (statusLog && found != lastFound) { lastFound = found; Debug.Log("[Ludo] DevAutoRun: search " + Mathf.RoundToInt(Time.realtimeSinceStartup - since) + "s, players found " + found + "/" + (RoomService.InRoom ? RoomService.MaxPlayers : size)); }
                }
            }
            var joining = create ? RoomService.CreatePrivateAsync(size, mode) : RoomService.JoinByCodeAsync(code);
            while (!joining.IsCompleted) yield return null;
            Debug.Log("[Ludo] DevAutoRun: join " + (joining.Result ? "ok" : "FAILED: " + RoomService.LastError));
            if (joining.Result && create) Debug.Log("[Ludo] DevAutoRun: ROOMCODE " + RoomService.Code);
            if (!joining.Result) yield break;
            router.Show(9);                                    // the waiting room; the host's Start button does the rest
            StartCoroutine(Housekeeping());

            if (voice)
            {
                var v = VoiceService.JoinAsync(RoomService.Code);
                while (!v.IsCompleted) yield return null;
                Debug.Log("[Ludo] DevAutoRun: voice " + (v.Result ? "ON" : "FAILED: " + VoiceService.LastError));
                while (true)
                {
                    yield return new WaitForSeconds(2f);
                    int inVoice = 0;
                    foreach (var p in RoomService.Players()) if (VoiceService.IsInVoice(p.Id)) inVoice++;
                    Debug.Log("[Ludo] DevAutoRun: people in voice channel = " + inVoice);
                }
            }
        }

        MatchLink logged;
        int chatsSent;
        bool rematchPressed;

        /// <summary>Every second: bind chat logging, press Ready, send the test chat lines, press Rematch.</summary>
        IEnumerator Housekeeping()
        {
            float roomSince = 0f;
            while (true)
            {
                yield return new WaitForSeconds(1f);
                var link = MatchLink.Current;
                if (link != logged)
                {
                    logged = link;
                    if (link != null) link.ChatReceived += m => Debug.Log("[Ludo] CHAT " + (m.mine ? "(me) " : "") + m.name + ": " + m.text);
                }
                var hud = FindFirstObjectByType<GameHud>();
                bool inGame = hud != null;
                if (!inGame && RoomService.InRoom)
                {
                    roomSince += 1f;
                    if (startAt > 0 && RoomService.IsHost && RoomService.Players().Count >= startAt && RoomService.AllReady())
                    {
                        var room = FindFirstObjectByType<RoomScreen>();
                        if (room != null) { Debug.Log("[Ludo] DevAutoRun: host presses Start"); room.StartMatch(); }
                    }
                    rematchPressed = false;
                    if (autoReady && !RoomService.IsHost && !RoomService.AmReady())
                    {
                        _ = RoomService.SetReadyAsync(true);
                        Debug.Log("[Ludo] DevAutoRun: pressed Ready");
                    }
                    if (chatText != null && chatsSent == 0 && roomSince > 3f && link != null && link.SendChat("room: " + chatText, out var p1)) { chatsSent = 1; Debug.Log("[Ludo] DevAutoRun: sent room chat"); }
                }
                if (inGame && link != null)
                {
                    roomSince = 0f;
                    if (dropAfter > 0f)
                    {
                        dropAfter -= 1f;
                        if (dropAfter <= 0f)
                        {
                            Debug.Log("[Ludo] DevAutoRun: cutting the connection now");
                            Unity.Netcode.NetworkManager.Singleton.Shutdown();
                        }
                    }
                    if (chatText != null && chatsSent == 1 && link.SendChat("match: " + chatText, out var p2)) { chatsSent = 2; Debug.Log("[Ludo] DevAutoRun: sent match chat"); }
                    if (rematch && hud.ResultVisible && !rematchPressed)
                    {
                        rematchPressed = true;
                        Debug.Log("[Ludo] DevAutoRun: pressing Rematch");
                        hud.RestartGame();
                    }
                }
            }
        }

        static async void AcceptAll()
        {
            foreach (var request in SocialService.IncomingRequests())
            {
                bool ok = await SocialService.AcceptAsync(request.Id);
                Debug.Log("[Ludo] DevAutoRun: accepted friend request from " + request.Name + " -> " + ok);
            }
        }
    }
}
#endif
