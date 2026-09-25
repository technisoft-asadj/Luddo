using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.AI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>One line of the waiting room: picture, name, a tag (HOST / YOU) and a small block button.</summary>
    [Serializable]
    public sealed class RoomSeatRow
    {
        public GameObject root;
        public Image avatar;
        public Image flag;                // the country the player chose (hidden if none)
        public Image seatColor;           // the seat's board colour dot
        public TMP_Text nameText;
        public TMP_Text tagText;
        public GameObject speakingGlow;   // lit while this player talks
        public Button moreButton;         // block / report someone else
    }

    /// <summary>
    /// The waiting room. Shows the room code (to share with friends), the four seats, the voice button and, for the
    /// host, the Start button. Public rooms start by themselves a few seconds after enough players joined.
    /// Leaving this screen (Back arrow, Android back) leaves the room - unless the match is starting.
    /// </summary>
    public sealed class RoomScreen : MonoBehaviour
    {
        [SerializeField] ScreenRouter router;
        [SerializeField] int friendsScreen = 10;
        [SerializeField] TMP_Text codeText;
        [SerializeField] TMP_Text hintText;                // "Share this code..." / "Waiting for the host" / countdown
        [SerializeField] TMP_Text noticeText;              // short messages: copied, errors
        [SerializeField] RoomSeatRow[] rows;               // 4 seats
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startLabel;
        [SerializeField] Button readyButton;               // guests of a private room: "Ready" / "Not ready"
        [SerializeField] TMP_Text readyLabel;
        [SerializeField] GameObject privateOnly;           // code card + invite/share buttons
        [SerializeField] Image micIcon;
        [SerializeField] TMP_Text micLabel;
        [SerializeField] Sprite micOn;
        [SerializeField] Sprite micOff;
        [SerializeField] Color speakingColor = new Color(0.3f, 1f, 0.45f);
        [SerializeField] GameObject moreModal;             // block / report pop-up
        [SerializeField] TMP_Text moreTitle;

        bool starting;
        bool keepRoom;                                      // true while the Friends screen is open on top (we come back to this room)
        bool leaving;
        float countdown = -1f;
        int lastPlayerCount;
        float nextGlow;
        List<RoomPlayer> shown = new List<RoomPlayer>();
        RoomPlayer moreTarget;

        void OnEnable()
        {
            starting = false;
            keepRoom = false;
            leaving = false;
            countdown = -1f;
            lastPlayerCount = 0;
            noticeText.text = "";
            moreModal.SetActive(false);
            RoomService.Changed += Refresh;
            RoomService.Closed += OnRoomClosed;
            VoiceService.Changed += RefreshVoice;
            PhotoService.Loaded += OnPhotoLoaded;
            Refresh();
            RefreshVoice();
            SocialService.SetBusy(false);
            if (RoomService.InRoom) _ = RoomService.RefreshMyPropertiesAsync();      // (after a match: not ready, new rating)
            else Invoke(nameof(GoBack), 0.2f);                                       // the room is gone (the host left): back to the menu
        }

        void GoBack()
        {
            if (this == null || !isActiveAndEnabled || leaving || RoomService.InRoom) return;
            leaving = true;
            router.Back();
        }

        void OnDisable()
        {
            RoomService.Changed -= Refresh;
            RoomService.Closed -= OnRoomClosed;
            VoiceService.Changed -= RefreshVoice;
            PhotoService.Loaded -= OnPhotoLoaded;
            if (starting || keepRoom) return;           // the match takes over the room; the Friends screen returns to it
            _ = RoomService.LeaveAsync();
            _ = VoiceService.LeaveAsync();
        }

        void OnPhotoLoaded(string playerId) => Refresh();

        void OnRoomClosed()
        {
            if (this == null || !isActiveAndEnabled || starting || leaving) return;
            leaving = true;
            router.Back();                              // the host closed the room
        }

        // ---------- what is shown ----------

        void Refresh()
        {
            if (!RoomService.InRoom) return;
            shown = RoomService.Players();
            bool host = RoomService.IsHost;
            bool isPublic = RoomService.IsPublic;

            codeText.text = RoomService.Code;
            privateOnly.SetActive(!isPublic);
            bool allReady = RoomService.AllReady();
            startButton.gameObject.SetActive(host);
            startButton.interactable = shown.Count >= 2 && allReady;    // real people only (at least two), and everybody ready
            startLabel.text = isPublic ? "Start Now" : "Start Game";
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(!host && !isPublic);   // the host starts the game; public rooms start by themselves
                readyLabel.text = RoomService.AmReady() ? "Ready!  (tap to undo)" : "I'm Ready";
            }
            int size = RoomService.MaxPlayers;
            string mode = (RoomService.IsRanked ? "RANKED  -  " : "") + GameSession.ModeName(RoomService.Mode).ToUpperInvariant() + "  -  " + size + " PLAYERS  -  ";
            if (shown.Count < 2) hintText.text = mode + (isPublic ? "Searching for players..." : "Share the code so a friend can join.");
            else if (!host) hintText.text = mode + (isPublic || RoomService.AmReady() ? "Waiting for the host to start..." : "Press Ready when you are set.");
            else if (!allReady) hintText.text = "Waiting for everybody to press Ready...";
            else hintText.text = isPublic ? "" : mode + "Everyone is ready!";

            int[] seats = Ludo.Core.Board.DefaultSeats(Mathf.Clamp(Mathf.Max(shown.Count, 2), 2, 4));
            int[] fullSeats = Ludo.Core.Board.DefaultSeats(Mathf.Clamp(size, 2, 4));
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                row.root.SetActive(i < size);                              // a 2-player room shows two seats
                bool filled = i < shown.Count;
                if (row.seatColor != null) row.seatColor.color = SeatStyle.Colors[i < fullSeats.Length && !filled ? fullSeats[i] : seats[Mathf.Min(i, seats.Length - 1)]];
                if (filled)
                {
                    var p = shown[i];
                    PhotoService.Ensure(p.Id, p.PhotoStamp);
                    row.avatar.enabled = true;
                    row.avatar.sprite = (p.IsMe ? ProfilePhoto.Mine : ProfilePhoto.ForPlayer(p.Id)) ?? AvatarLibrary.Get(p.Avatar);
                    row.nameText.text = p.Name;
                    row.nameText.color = Color.white;
                    FlagLibrary.Apply(row.flag, p.IsMe ? GameSettings.Country : p.Country);     // each player's own lobby data
                    string who = p.IsHost ? (p.IsMe ? "HOST - YOU" : "HOST") : p.IsMe ? "YOU" : "";
                    string level = Ludo.Core.Rating.Tier(p.Rating) + " " + p.Rating;
                    string state = isPublic || p.IsHost ? "" : (p.Ready ? "READY" : "not ready");
                    string tag = who.Length > 0 ? who + "  |  " + level : level;
                    row.tagText.text = state.Length > 0 ? tag + "  |  " + state : tag;
                    row.moreButton.gameObject.SetActive(!p.IsMe);
                }
                else
                {
                    row.avatar.enabled = false;
                    FlagLibrary.Apply(row.flag, "");
                    row.nameText.text = "Waiting for a player...";
                    row.nameText.color = new Color(1f, 1f, 1f, 0.5f);
                    row.tagText.text = "";
                    row.moreButton.gameObject.SetActive(false);
                }
                row.speakingGlow.SetActive(false);
            }

            // a newcomer restarts a public room's countdown, so people have time to join
            if (shown.Count != lastPlayerCount)
            {
                lastPlayerCount = shown.Count;
                countdown = -1f;
            }
        }

        void RefreshVoice()
        {
            switch (VoiceService.Status)
            {
                case VoiceService.VoiceStatus.Starting: micLabel.text = "Connecting..."; micIcon.sprite = micOff; break;
                case VoiceService.VoiceStatus.On:
                    micLabel.text = VoiceService.MicMuted ? "Mic muted" : "Mic on";
                    micIcon.sprite = VoiceService.MicMuted ? micOff : micOn;
                    break;
                default: micLabel.text = "Voice chat"; micIcon.sprite = micOff; break;
            }
        }

        void Update()
        {
            if (!RoomService.InRoom || starting) return;
            var link = MatchLink.Current;

            // guests: the host said "go"
            if (link != null && link.TryTakeStart(out var start)) { BeginMatch(start); return; }

            // public room, host: start by itself
            if (RoomService.IsHost && RoomService.IsPublic && shown.Count >= 2)
            {
                if (countdown < 0f) countdown = shown.Count >= RoomService.MaxPlayers ? 3f : 15f;
                countdown -= Time.deltaTime;
                hintText.text = "Starting in " + Mathf.CeilToInt(Mathf.Max(0f, countdown));
                if (countdown <= 0f) StartMatch();
            }

            // talking glow
            if (Time.unscaledTime >= nextGlow)
            {
                nextGlow = Time.unscaledTime + 0.15f;
                for (int i = 0; i < rows.Length && i < shown.Count; i++)
                    rows[i].speakingGlow.SetActive(VoiceService.IsSpeaking(shown[i].Id));
            }
        }

        // ---------- buttons ----------

        public void Copy()
        {
            GUIUtility.systemCopyBuffer = RoomService.Code;
            Notice("Code copied");
        }

        public void Share()
        {
            var config = OnlineConfig.Load();
            string link = config != null && !string.IsNullOrEmpty(config.storeUrl) ? "\n" + config.storeUrl : "";
            NativeShare.Text("Ludo Fight", "Join my Ludo Fight room! Code: " + RoomService.Code + link);
        }

        public void InviteFriends()
        {
            keepRoom = true;
            router.Show(friendsScreen);
        }

        public void ToggleVoice()
        {
            if (VoiceService.Status == VoiceService.VoiceStatus.Starting) return;
            if (VoiceService.IsOn) VoiceService.SetMicMuted(!VoiceService.MicMuted);
            else JoinVoice();
        }

        async void JoinVoice()
        {
            bool ok = await VoiceService.JoinAsync(RoomService.Code);
            if (this == null) return;
            if (!ok) Notice(VoiceService.LastError);
        }

        public async void ToggleReady()
        {
            if (readyButton != null) readyButton.interactable = false;
            bool ok = await RoomService.SetReadyAsync(!RoomService.AmReady());
            if (this == null) return;
            if (readyButton != null) readyButton.interactable = true;
            if (!ok) Notice("Could not change your ready state. Try again.");
            Refresh();
        }

        public void StartMatch()
        {
            if (starting || !RoomService.IsHost) return;
            var link = MatchLink.Current;
            if (link == null) return;
            if (!RoomService.AllReady()) { Notice("Waiting for everybody to be ready."); return; }
            var start = RoomService.BuildStart();
            if (start == null) { Notice("You need at least two players."); return; }
            _ = RoomService.LockAsync();
            link.BroadcastStart(start);
            BeginMatch(start);
        }

        void BeginMatch(MatchStart start)
        {
            starting = true;
            MatchStarter.Begin(start);
        }

        // ---------- block / report ----------

        public void OpenMore(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= shown.Count) return;
            moreTarget = shown[rowIndex];
            moreTitle.text = moreTarget.Name;
            moreModal.SetActive(true);
        }

        public void CloseMore() => moreModal.SetActive(false);

        public async void AddFriendTarget()
        {
            moreModal.SetActive(false);
            Notice("Sending friend request...");
            string message = await SocialService.RequestFromGameAsync(moreTarget.Id);
            if (this == null) return;
            Notice(moreTarget.Name + ": " + message);
        }

        public void MuteTarget()
        {
            VoiceService.ToggleMuteFor(moreTarget.Id);
            Notice(VoiceService.IsMutedByMe(moreTarget.Id) ? moreTarget.Name + " is muted for you" : moreTarget.Name + " can be heard again");
            moreModal.SetActive(false);
        }

        public void BlockTarget()
        {
            SafetyService.Block(moreTarget.Id);
            Notice(moreTarget.Name + " is blocked");
            moreModal.SetActive(false);
        }

        public void ReportTarget()
        {
            bool sent = SafetyService.Report(moreTarget.Id, moreTarget.Name, "Inappropriate behaviour");
            Notice(sent ? "Thank you. Your email app opened so you can send the report." : "Reporting is not set up yet.");
            SafetyService.Block(moreTarget.Id);         // reporting someone also hides them from you
            moreModal.SetActive(false);
        }

        void Notice(string message)
        {
            noticeText.text = message;
            CancelInvoke(nameof(ClearNotice));
            Invoke(nameof(ClearNotice), 3f);
        }

        void ClearNotice() { if (noticeText != null) noticeText.text = ""; }
    }
}
