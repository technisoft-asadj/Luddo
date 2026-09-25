using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;
using Ludo.Services;

namespace Ludo.Game
{
    /// <summary>
    /// Everything drawn on top of the board while playing: player badges, the pause button and menu, the result
    /// screen and short messages. GameController tells it WHAT to show; it never touches the game rules.
    /// The Android back button pauses/resumes the game (or leaves it from the result screen).
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] GameController controller;
        [SerializeField] PlayerBadge[] badges;          // index = seat: Red, Green, Yellow, Blue
        [SerializeField] GameObject pausePanel;
        [SerializeField] GameObject resultPanel;
        [SerializeField] TMP_Text resultTitle;
        [SerializeField] Image resultAvatar;            // the winner's picture on the podium
        [SerializeField] Image resultRing;              // ring round it in the winner's colour
        [SerializeField] Image resultFlag;              // online: the winner's country flag (hidden if none)
        [SerializeField] ToastBanner toast;
        [SerializeField] TMP_Text turnText;             // "Player 1's turn" in the top banner
        [SerializeField] Image turnAvatar;              // the current player's picture in the banner
        [SerializeField] Image turnAvatarBg;            // circle behind it, in the current player's colour
        [SerializeField] RectTransform pendingRow;      // Ludo Star rule: small dice of the numbers still to play (6, 5 ...)
        [SerializeField] Image[] pendingDice;
        [SerializeField] RectTransform valueChooser;    // "which number first?" buttons above the tapped pawn
        [SerializeField] Button[] valueButtons;
        [SerializeField] Image[] valueDice;
        [SerializeField] Sprite[] diceFaces;            // flat dice pictures 1..6 (index 0 = one pip)
        [SerializeField] TMP_Text modeText;             // "MASTER MODE - capture an opponent before ..." (hidden in Classic)
        [SerializeField] TMP_Text countdownText;        // the 3 - 2 - 1 - GO! that opens a match
        [SerializeField] GameObject friendModal;        // online: tap a player's badge to send them a friend request
        [SerializeField] TMP_Text friendTitle;
        [SerializeField] TMP_Text friendMessage;
        [SerializeField] GameObject friendAddButton;
        [SerializeField] Button autoButton;             // online only: let the game play this player's turns
        [SerializeField] Image autoImage;
        [SerializeField] TMP_Text autoLabel;            // "Auto: OFF" / "Auto: ON"
        [SerializeField] GameObject[] offlineOnly;      // buttons that make no sense in an online match (Play Again, Restart)

        [SerializeField] TurnTimerBar timer;            // online: time left for the current turn
        [SerializeField] TMP_Text resultDetail;         // online: "Rating +14" under the winner's name
        [SerializeField] GameObject playAgainButton;    // offline: Play Again; online: Rematch (back to the waiting room)
        [SerializeField] TMP_Text playAgainLabel;

        // online result: the reward card and the parts that move to make room for it
        [SerializeField] ResultCard resultCard;
        [SerializeField] RectTransform trophyRt, podiumRt, titleRt, playAgainRt, menuRt;
        [SerializeField] TMP_Text menuLabel;
        readonly System.Collections.Generic.Dictionary<RectTransform, Vector2> offlineLayout = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();
        MatchSummary summary;                           // the online result on screen (null offline)
        bool adBusy;                                    // a rewarded ad is on screen
        float nextAdCheck;

        bool leaving;                                   // true while an interstitial is on screen after the game
        float nextVoiceCheck;

        public bool Paused => pausePanel.activeSelf;
        public bool ResultVisible => resultPanel != null && resultPanel.activeSelf;

        int turnSeat = -1;

        /// <summary>
        /// The "3 - 2 - 1 - GO!" that opens a match, so nobody is thrown straight into their first turn. Each number pops in
        /// big and shrinks away; GO! is shorter. Unscaled time is deliberate: the board is not playing yet.
        /// </summary>
        public System.Collections.IEnumerator PlayCountdown()
        {
            if (countdownText == null) yield break;
            string[] steps = { "3", "2", "1", "GO!" };
            countdownText.gameObject.SetActive(true);
            var rt = countdownText.rectTransform;
            for (int i = 0; i < steps.Length; i++)
            {
                bool go = i == steps.Length - 1;
                countdownText.text = steps[i];
                // gold numbers, green GO!: both read over the board's white centre thanks to the font's dark outline
                countdownText.color = go ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.83f, 0.15f);
                AudioService.Play(go ? SfxId.Home : SfxId.Click);
                float hold = go ? 0.45f : 0.6f;
                for (float t = 0f; t < hold;)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / hold);
                    rt.localScale = Vector3.one * Mathf.Lerp(1.55f, 0.95f, k * k);
                    countdownText.alpha = k > 0.75f ? Mathf.InverseLerp(1f, 0.75f, k) : 1f;
                    yield return null;
                }
            }
            countdownText.alpha = 1f;
            rt.localScale = Vector3.one;
            countdownText.gameObject.SetActive(false);
        }

        /// <summary>The time left for the current turn, shown as a bar on that player's name tag.</summary>
        public void ShowTimer(float remaining, float total)
        {
            if (timer != null) timer.Show(remaining, total);
            for (int i = 0; i < badges.Length; i++)
            {
                if (i == turnSeat) badges[i].ShowTimer(remaining, total);
                else badges[i].HideTimer();
            }
        }

        public void HideTimer()
        {
            if (timer != null) timer.Hide();
            foreach (var b in badges) b.HideTimer();
        }

        // the screen must not dim while the player is thinking or the CPUs are playing
        void Start()
        {
            // an online game cannot be restarted or frozen: the other players keep playing
            if (GameSession.IsOnline && offlineOnly != null)
                foreach (var o in offlineOnly) if (o != null) o.SetActive(false);
        }

        void OnEnable() => Screen.sleepTimeout = SleepTimeout.NeverSleep;

        void OnDisable() => Screen.sleepTimeout = SleepTimeout.SystemSetting;

        /// <summary>A call, the home button or the app switcher: pause the game so nothing happens behind the player's back.</summary>
        void OnApplicationPause(bool paused)
        {
            if (paused && !Paused && !resultPanel.activeSelf && !GameSession.IsOnline) Pause();
        }

        void Update()
        {
            // online voice chat: light up the badge of whoever is talking
            if (GameSession.IsOnline && Time.unscaledTime >= nextVoiceCheck)
            {
                nextVoiceCheck = Time.unscaledTime + 0.15f;
                for (int seat = 0; seat < badges.Length; seat++) badges[seat].SetSpeaking(controller.SeatIsSpeaking(seat));
            }

            PollRewardedAd();

            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (resultPanel.activeSelf) GoToMenu();
            else if (Paused) Resume();
            else Pause();
        }

        // ---------- called by GameController ----------

        // ---------- Ludo Star rule: several numbers per turn ----------

        int chosenValue;
        readonly int[] choiceValues = new int[3];

        void Awake()
        {
            if (valueButtons == null) return;
            for (int i = 0; i < valueButtons.Length; i++)
            {
                int index = i;
                valueButtons[i].onClick.AddListener(() => { chosenValue = choiceValues[index]; HideValueChoice(); });
            }
        }

        /// <summary>
        /// The numbers rolled this turn that still have to be played, as small dice next to the PLAYER whose turn it is (not
        /// the dice tray - the tray stays in its own fixed spot; this follows whichever badge is glowing).
        /// </summary>
        public void ShowPendingRolls(System.Collections.Generic.IReadOnlyList<int> values, int seat)
        {
            if (pendingRow == null) return;
            bool show = values != null && values.Count > 1 && seat >= 0 && seat < badges.Length;
            pendingRow.gameObject.SetActive(show);
            if (!show) return;
            for (int i = 0; i < pendingDice.Length; i++)
            {
                bool used = i < values.Count;
                pendingDice[i].gameObject.SetActive(used);
                if (used) pendingDice[i].sprite = diceFaces[Mathf.Clamp(values[i], 1, 6) - 1];
            }
            // just outside the player's own badge (above it for the top two seats, below it for the bottom two). Both rects'
            // sizes are in local (pixel) units, but .position is world space - a Screen Space Camera canvas's own transform
            // is scaled way down (pixels -> world units), so the pixel offset must be scaled the same way before it is added.
            var badgeRt = (RectTransform)badges[seat].transform;
            Vector3 centre = badgeRt.TransformPoint(badgeRt.rect.center);   // badges pivot on a corner, not their middle
            bool top = seat == (int)Seat.Red || seat == (int)Seat.Green;
            float dir = top ? -1f : 1f;
            float gap = 34f;
            float scale = badgeRt.lossyScale.y;
            Vector3 offset = new Vector3(0f, dir * (badgeRt.rect.height * 0.5f + pendingRow.rect.height * 0.5f + gap) * scale, 0f);
            pendingRow.position = centre + offset;
        }

        /// <summary>A pawn can move with more than one of the numbers: ask which (shown above the pawn).</summary>
        public void AskValue(Vector3 pawnWorld, System.Collections.Generic.IList<int> values)
        {
            chosenValue = 0;
            for (int i = 0; i < valueButtons.Length; i++)
            {
                bool used = i < values.Count;
                valueButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                choiceValues[i] = values[i];
                valueDice[i].sprite = diceFaces[Mathf.Clamp(values[i], 1, 6) - 1];
            }
            valueChooser.gameObject.SetActive(true);
            PlaceAt(valueChooser, pawnWorld, new Vector2(0f, 1.6f));
        }

        /// <summary>The number the player picked in the chooser (0 = none yet). Reading it clears it.</summary>
        public int TakeChosenValue()
        {
            int v = chosenValue;
            chosenValue = 0;
            return v;
        }

        // ---------- add a friend from the match ----------

        string friendTargetId = "";

        /// <summary>A player's badge was tapped (wired with the seat): offer to add them as a friend if they are a real person.</summary>
        public void OpenPlayerCard(int seat)
        {
            if (friendModal == null || controller == null || GameSession.AddFriend == null) return;
            string id = controller.OnlineIdOfSeat(seat);
            if (string.IsNullOrEmpty(id)) return;                    // me, the computer, or an offline game
            friendTargetId = id;
            friendTitle.text = badges[seat].PlayerName;
            friendMessage.text = "Not friends yet? Send a friend request.";
            friendAddButton.SetActive(true);
            friendModal.SetActive(true);
        }

        public async void TapAddFriend()
        {
            if (string.IsNullOrEmpty(friendTargetId) || GameSession.AddFriend == null) return;
            friendAddButton.SetActive(false);
            friendMessage.text = "Sending...";
            string message = await GameSession.AddFriend(friendTargetId);
            if (this == null) return;
            friendMessage.text = message;
        }

        public void CloseFriend() { if (friendModal != null) friendModal.SetActive(false); }

        // ---------- auto play ----------

        /// <summary>True while the player has handed their turns to auto play (rolls and moves for them).</summary>
        public bool AutoOn { get; private set; }

        /// <summary>Show the Auto button (online games) and switch auto play off, e.g. for a new match.</summary>
        public void SetAutoAvailable(bool available)
        {
            AutoOn = false;
            if (autoButton != null) autoButton.gameObject.SetActive(available);
            ShowAuto();
        }

        /// <summary>Wired to the Auto button: hand the turns over, or take them back.</summary>
        public void TapAuto()
        {
            AutoOn = !AutoOn;
            ShowAuto();
        }

        void ShowAuto()
        {
            if (autoLabel != null) autoLabel.text = AutoOn ? "Auto: ON" : "Auto: OFF";
            if (autoImage != null) autoImage.color = AutoOn ? new Color(0.55f, 1f, 0.6f) : Color.white;
        }

        public void HideValueChoice()
        {
            if (valueChooser != null) valueChooser.gameObject.SetActive(false);
        }

        /// <summary>Put a HUD element over a point of the board (the HUD canvas is drawn by the same orthographic camera).</summary>
        void PlaceAt(RectTransform rt, Vector3 world, Vector2 offsetWorld)
        {
            var p = world + (Vector3)offsetWorld;
            rt.position = new Vector3(p.x, p.y, rt.position.z);
            // keep it on screen
            var canvasRt = (RectTransform)rt.parent;
            Vector3 local = rt.localPosition;
            var r = canvasRt.rect; var size = rt.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, r.xMin + size.x + 10f, r.xMax - size.x - 10f);
            local.y = Mathf.Clamp(local.y, r.yMin + size.y + 10f, r.yMax - size.y - 10f);
            rt.localPosition = local;
        }

        /// <summary>Show which game mode is being played and its one special rule (nothing for Classic).</summary>
        public void SetMode(GameMode mode)
        {
            if (modeText == null) return;
            modeText.gameObject.SetActive(mode != GameMode.Classic);
            modeText.text = "<color=#FFD426>" + GameSession.ModeName(mode).ToUpperInvariant() + " MODE</color>   " + GameSession.ModeRule(mode);
        }

        public void HideBadges()
        {
            foreach (var b in badges) b.Hide();
        }

        public void SetBadge(int seat, string playerName, string subtitle, Sprite avatar, Sprite flag = null) =>
            badges[seat].Setup(playerName, subtitle, SeatStyle.Colors[seat], avatar, flag);

        /// <summary>A short message in the top banner (e.g. "Connection lost - reconnecting..."); empty text shows the turn again.</summary>
        public void SetStatus(string text)
        {
            if (turnText == null || string.IsNullOrEmpty(text)) return;
            turnText.text = text;
        }

        public void SetTurnSeat(int seat)
        {
            turnSeat = seat;
            for (int i = 0; i < badges.Length; i++) badges[i].SetTurn(i == seat);
            if (turnText != null) turnText.text = badges[seat].PlayerName + "'s turn";
            if (turnAvatar != null) { turnAvatar.sprite = badges[seat].Avatar; turnAvatar.enabled = turnAvatar.sprite != null; }
            if (turnAvatarBg != null) turnAvatarBg.color = SeatStyle.Colors[seat];
        }

        public void ShowToast(ToastKind kind, float seconds)
        {
            toast.Show(kind, seconds);
            if (kind == ToastKind.ThreeSixes)
            {
                AudioService.Play(SfxId.Error);      // a lost turn is worth a sound and a small buzz
                Haptics.Pulse(60);
            }
        }

        public void ShowResult(string winnerName, Color color, Sprite avatar, MatchSummary online = null, bool winnerIsMe = false, bool byForfeit = false, Sprite flag = null)
        {
            toast.Hide();
            summary = online;
            resultRing.color = color;
            resultAvatar.sprite = avatar;
            resultAvatar.enabled = avatar != null;
            ShowResultFlag(flag);
            if (resultDetail != null) resultDetail.gameObject.SetActive(false);

            if (online == null)
            {
                resultTitle.text = winnerName + " Wins!";
                UseOnlineLayout(false);
            }
            else
            {
                switch (online.Result)
                {
                    case ResultKind.Win:
                        resultTitle.text = online.ByForfeit ? "WIN BY FORFEIT" : "VICTORY!";
                        resultCard.Show(online, online.ByForfeit ? "Your opponent did not reconnect." : "");
                        break;
                    case ResultKind.Loss:
                        resultTitle.text = "DEFEAT";
                        resultCard.Show(online, online.Mode == MatchMode.Casual && !winnerIsMe && winnerName.StartsWith("CPU")
                            ? "The computer finished the match for a player who left."
                            : winnerName + " won.   Better luck next time!");
                        break;
                    default:                                    // my own connection was gone
                        resultTitle.text = "MATCH FORFEITED";
                        resultCard.Show(online, "Your connection was lost.");
                        break;
                }
                UseOnlineLayout(true);
            }
            ShowPlayAgain(true);
            resultPanel.SetActive(true);
            AdsService.NotifyGameFinished();     // counts toward "an ad is due" (the ad itself only ever comes AFTER this screen)
            AudioService.Play(online != null && online.Result != ResultKind.Win ? SfxId.Error : SfxId.Win);
            AudioService.DuckMusic(3f);          // let the jingle be heard over the music
            Haptics.Buzz();
        }

        /// <summary>The game cannot go on. Online with a summary this is a forfeit: the card shows what it cost.</summary>
        public void ShowEnded(string message, MatchSummary forfeit = null)
        {
            toast.Hide();
            summary = forfeit;
            if (resultDetail != null) resultDetail.gameObject.SetActive(false);
            resultAvatar.enabled = false;
            ShowResultFlag(null);
            resultRing.color = new Color(1f, 1f, 1f, 0.35f);
            if (forfeit != null)
            {
                resultTitle.text = "MATCH FORFEITED";
                resultCard.Show(forfeit, message == "Connection lost" ? "You lost connection and did not reconnect in time." : "You were away too long and left the match.");
                UseOnlineLayout(true);
            }
            else
            {
                resultTitle.text = message;
                UseOnlineLayout(false);
            }
            ShowPlayAgain(false);                    // the room is gone: nothing to play again with
            resultPanel.SetActive(true);
        }

        /// <summary>Online results need room for the reward card: the trophy goes, the podium and buttons move.</summary>
        void UseOnlineLayout(bool on)
        {
            if (resultCard == null) return;
            if (offlineLayout.Count == 0)
                foreach (var rt in new[] { trophyRt, podiumRt, titleRt, playAgainRt, menuRt })
                    if (rt != null) offlineLayout[rt] = rt.anchoredPosition;
            resultCard.gameObject.SetActive(on);
            if (menuLabel != null) menuLabel.text = on ? "Continue" : "Main Menu";
            if (trophyRt != null) trophyRt.gameObject.SetActive(!on);
            if (podiumRt != null) { podiumRt.anchoredPosition = on ? new Vector2(0f, 700f) : offlineLayout[podiumRt]; podiumRt.localScale = on ? Vector3.one * 0.8f : Vector3.one; }
            if (titleRt != null) titleRt.anchoredPosition = on ? new Vector2(0f, 470f) : offlineLayout[titleRt];
            if (playAgainRt != null) playAgainRt.anchoredPosition = on ? new Vector2(0f, -580f) : offlineLayout[playAgainRt];
            if (menuRt != null) menuRt.anchoredPosition = on ? new Vector2(0f, -750f) : offlineLayout[menuRt];
            adBusy = false;
            nextAdCheck = 0f;
        }

        /// <summary>The "watch ad" button. The bonus is added only if the ads SDK confirms the reward; nothing is lost otherwise.</summary>
        public void WatchAd()
        {
            if (adBusy || leaving || summary == null || !summary.CanOfferAd) return;
            adBusy = true;
            AdsService.ShowRewarded(earned =>
            {
                adBusy = false;
                if (this == null) return;
                if (!earned) { resultCard.SetNote("The ad was not completed - no bonus this time."); return; }
                var updated = GameSession.Settler?.ClaimAdBonus();      // once per match: a second claim does nothing
                if (updated == null) return;
                summary = updated;
                resultCard.Show(updated, updated.ByForfeit ? "Your opponent did not reconnect." : "");
                AudioService.Play(SfxId.Win);
            });
        }

        void PollRewardedAd()
        {
            // the rewarded ad loads in the background: offer it as soon as it is ready, or say it is unavailable
            if (summary != null && resultPanel.activeSelf && summary.CanOfferAd && !adBusy && Time.unscaledTime >= nextAdCheck)
            {
                nextAdCheck = Time.unscaledTime + 0.5f;
                resultCard.RefreshAd(summary, AdsService.RewardedReady);
            }
        }

        void ShowPlayAgain(bool possible)
        {
            if (playAgainButton != null) playAgainButton.SetActive(possible);
            if (playAgainLabel != null) playAgainLabel.text = GameSession.IsOnline ? "Rematch" : "Play Again";
        }

        public void HideResult() => resultPanel.SetActive(false);

        void ShowResultFlag(Sprite flag)
        {
            if (resultFlag == null) return;
            resultFlag.sprite = flag;
            resultFlag.gameObject.SetActive(flag != null);
        }

        // ---------- buttons (wired in the Inspector) ----------

        public void Pause()
        {
            pausePanel.SetActive(true);
            if (!GameSession.IsOnline) Time.timeScale = 0f;          // freezes every animation and timer (never online: the others keep playing)
        }

        public void Resume()
        {
            pausePanel.SetActive(false);
            Time.timeScale = 1f;
        }

        /// <summary>Play Again / Restart. From the result screen an interstitial ad may come first (never mid-game).</summary>
        public void RestartGame()
        {
            if (leaving) return;
            if (GameSession.IsOnline)
            {
                // rematch: back to the waiting room with the same people (the room and the voice chat stay open)
                RunAfterPossibleAd(() =>
                {
                    Time.timeScale = 1f;
                    GameSession.ReturnToRoom = true;
                    SceneLoader.Load(SceneLoader.Menu);
                });
                return;
            }
            RunAfterPossibleAd(() =>
            {
                Resume();
                HideResult();
                controller.StartGame(controller.PlayerCount);
            });
        }

        /// <summary>Main Menu. From the result screen an interstitial ad may come first.</summary>
        public void GoToMenu()
        {
            if (leaving) return;
            RunAfterPossibleAd(() =>
            {
                Time.timeScale = 1f;
                GameSession.LeaveOnline?.Invoke();       // online: leave the room, so the others are told
                GameSession.ClearOnline();
                SceneLoader.Load(SceneLoader.Menu);
            });
        }

        /// <summary>
        /// An ad is only ever offered when the game is over (the result screen is showing). Leaving mid-game through
        /// the pause menu never shows one. 'leaving' blocks double taps while the ad is on screen.
        /// </summary>
        void RunAfterPossibleAd(System.Action next)
        {
            if (!resultPanel.activeSelf) { next(); return; }
            leaving = true;
            AdsService.ShowInterstitialIfDue(() => { leaving = false; next(); });
        }
    }
}
