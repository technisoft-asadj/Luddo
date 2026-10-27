using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Ludo.AI;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>What the screen is doing right now. Input is only accepted in the state that needs it.</summary>
    public enum FlowState { WaitingForRoll, Rolling, SelectingToken, Moving, GameOver }

    /// <summary>
    /// Runs a whole game on screen: ties the Core engine (LudoGame) to the views (board, dice, HUD) and the
    /// player's taps. The engine decides what is allowed; this class decides WHEN things happen and shows them.
    /// The turn is written as one readable coroutine: wait for tap -> roll -> choose -> move -> repeat.
    /// Who plays each seat comes from the menus (GameSession); opened alone in the Editor it uses the Inspector defaults.
    ///
    /// Online: every phone runs this same code with the same engine. The HOST decides the dice values and checks
    /// the moves; the others say "roll" / "I pick move N" and then read what the host announced. Because the engine is
    /// deterministic, announcing "dice = 4" and "move 2" keeps all the phones in exactly the same position.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] RulesConfigAsset rules;
        [SerializeField] BoardView board;
        [SerializeField] DiceView dice;
        [SerializeField] GameHud hud;
        [SerializeField, Range(2, 4)] int playerCount = 4;
        [SerializeField] PlayerSlot[] slots =                      // who plays each seat (index = player number)
        {
            PlayerSlot.Human, PlayerSlot.Cpu(AiDifficulty.Easy), PlayerSlot.Cpu(AiDifficulty.Medium), PlayerSlot.Cpu(AiDifficulty.Hard)
        };
        [SerializeField] float aiThinkSeconds = 0.7f;              // pause before the computer rolls, so you can follow
        [SerializeField] float tapRadius = 1.1f;                   // how close (in cells) a tap must be to a token or the dice
        [SerializeField] float turnSecondsSetting = 20f;                  // online: time to roll, and again time to pick a pawn
        const float hostGraceSeconds = 2f;                           // online host: waits a little longer than the player's own phone, so a last-second tap still arrives
        [SerializeField] int missesBeforeKick = 3;                 // online: this many idle turns in a row and the computer takes the seat

        LudoGame game;
        readonly int[] missedTurns = new int[4];                   // idle turns in a row, per player (online)
        float timerStart;                                          // when the current time limit began
        bool timerActive;                                          // a human's time limit is running (online only)
        bool waitTimedOut;                                         // the last wait ended because the time ran out
        bool leftForInactivity;
        bool ending;                                             // the match is being settled: nothing else may end it
        string awayShown;                                        // the "X lost connection" line currently on the banner
        IAiPlayer[] ais;                                           // null entry = a human plays that player
        Camera cam;
        Vector3 lastTap;

        IMatchLink link;                                           // null in an offline game
        QueuedDiceSource queuedDice;                               // online: dice values announced by the host
        RandomDiceSource hostDice;                                 // online host: where the values come from
        int rolledValue;                                           // result of ObtainRoll (online)
        int requestedPick;                                         // result of a remote player's pick request (online host)
        Move chosenMove;                                           // result of ChooseOnline

        [SerializeField] float undoWindowSeconds = 2.6f;            // how long the Undo button stays up after a move ends the turn
        int undosLeft;                                             // takebacks still available this match (0 online: see UndoOffered)
        LudoGame.Snapshot undoPoint;                               // the position just before the local person's last move
        bool undoRequested;

        RulesConfig activeRules;                                   // the rules asset with the chosen game mode (Classic / Master / Arrow / Blitz)
        RulesConfig Rules => activeRules ??= rules.Rules.WithMode(GameSession.Mode);

        /// <summary>Online: the account ID of the person on this seat, or "" for me, the computer or an offline game.</summary>
        public string OnlineIdOfSeat(int seat)
        {
            if (link == null || game == null) return "";
            for (int p = 0; p < playerCount; p++)
                if (game.State.SeatOf(p) == seat && slots[p].isRemote && !slots[p].isAi) return slots[p].onlineId ?? "";
            return "";
        }

        public FlowState State { get; private set; }
        public GameMode Mode => Rules.Mode;
        public int PlayerCount => playerCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Tests only (Editor and Development builds, never a release): the local person's turns play themselves.
        /// A test build starts with "-autoplay" on the command line.</summary>
        public static bool AutoPlay;

        /// <summary>Tests only: a shorter turn time ("-turnseconds 6") so time-outs can be tested quickly.</summary>
        public static float TestTurnSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ReadTestFlags()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoplay") AutoPlay = true;
                if (args[i] == "-turnseconds" && i + 1 < args.Length && float.TryParse(args[i + 1], out float s)) TestTurnSeconds = s;
            }
        }
#endif

        /// <summary>Online: seconds a person has to roll, and again to pick a pawn.</summary>
        float TurnSeconds
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (TestTurnSeconds > 0f) return TestTurnSeconds;
#endif
                return turnSecondsSetting;
            }
        }

        // Only compiled into the Editor, Development builds and builds that define LUDO_TRACE,
        // so release builds carry no log calls (remove LUDO_TRACE from Player Settings before release).
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("LUDO_TRACE")]
        static void Log(string message) => Debug.Log("[Ludo] " + message);

        void Start()
        {
            cam = Camera.main;
            if (GameSession.IsConfigured)
            {
                playerCount = GameSession.PlayerCount;
                slots = (PlayerSlot[])GameSession.Slots.Clone();      // a copy: a player who leaves online becomes a computer
            }
            link = GameSession.Link;
            StartGame(playerCount);
        }

        public void StartGame(int players)
        {
            StopAllCoroutines();
            dice.Cancel();                                            // a throw from the abandoned match must not tumble over the new one
            playerCount = players;
            activeRules = rules.Rules.WithMode(GameSession.Mode);
            if (link != null)
            {
                queuedDice = new QueuedDiceSource();
                hostDice = new RandomDiceSource();
                game = new LudoGame(Board.DefaultSeats(players), Rules, queuedDice);
            }
            else
            {
                queuedDice = null;
                game = new LudoGame(Board.DefaultSeats(players), Rules, new RandomDiceSource());
            }
            ais = new IAiPlayer[players];
            // Undo is an offline convenience only. Online it would have to be validated and replayed by the host on every
            // phone, and a client that rewound by itself would simply desync - so it is switched off there, not faked.
            undosLeft = 0;
            undoPoint = null;
            undoRequested = false;
            hud.SetAutoAvailable(link != null);
            hud.HideBadges();
            hud.SetMode(Rules.Mode);
            for (int p = 0; p < players; p++)
            {
                PlayerSlot slot = p < slots.Length ? slots[p] : PlayerSlot.Human;
                if (slot.isAi) ais[p] = AiFactory.Create(slot.difficulty, Environment.TickCount + p);   // same engine, same legal moves as a human
                hud.SetBadge(game.State.SeatOf(p), GameSession.NameOf(slot, p), GameSession.SubtitleOf(slot, game.State.SeatOf(p), MySeat()), GameSession.AvatarOf(slot, p), GameSession.FlagOf(slot));
            }
            board.Bind(game);
            hud.HideResult();
            diceSeat = game.State.SeatOf(game.CurrentPlayer);          // the dice starts in the first player's corner
            dicePlaced = false;
            SetupPads();
            StartCoroutine(Flow());
        }

        /// <summary>Keep the dice's tray in the open space between the board's bottom-corner badges and the screen edge,
        /// whatever the phone's shape. A fixed spot (rather than one that hunts for room next to whichever badge is on
        /// top) can never collide with the turn banner or the mode line above the board; whose turn it is shows on the
        /// dice itself (see DiceView.ShowSeatIcon). Two hard limits, measured in the Editor via RectTransform.GetWorldCorners
        /// and SpriteRenderer.bounds at this project's test tablet's aspect (a short/wide screen, the tight case - a taller
        /// phone aspect always leaves more room than this): the tray must stay below the badges' bottom edge, and above the
        /// screen's own bottom edge. On the tablet those two limits leave under 3 world units of gap - too little for the
        /// tray's old, bigger art (see UiSceneBuilder.BuildDiceTray), which is why the art itself was shrunk to fit.</summary>
        void LateUpdate()
        {
            if (cam == null) return;
            float half = BoardGrid.Size * 0.5f;
            // the badges are UI on a Screen-Space-Camera canvas, so their world-space drop below the board edge scales
            // with the camera's zoom, not a fixed world distance - measured on the test tablet (aspect 0.625, ortho
            // 13.6): badges hang 3.18 units below the board edge, i.e. ~0.24x orthographicSize.
            float badgeBottom = half + 0.24f * cam.orthographicSize;
            const float trayHalfHeight = 1.25f, trayOffset = 0.09f;   // matches the "Shadow" panel's own size/offset
            float yMin = badgeBottom + trayHalfHeight - trayOffset;          // any higher and the tray touches the badges
            float yMax = cam.orthographicSize - trayHalfHeight - trayOffset; // any lower and the tray clips off-screen
            float y = yMax >= yMin ? (yMin + yMax) * 0.5f : yMin;   // centred in the gap; badge clearance wins if it's too tight
            // Every player's own corner slot fits on this screen: online it also decides where the Auto button and the chat message
            // bar go (see PlaceOnlineExtras). Otherwise (a wide tablet) the old single dice at the bottom is used.
            bool corners = SeatDice && AllSeatSpotsFit();
            if (link != null && !corners)
            {
                // no corner slots: the chat message bar goes above the dice, so the dice sits as low as the screen allows -
                // above the phone's gesture bar - to leave the most room between it and the badges
                float safeBottom = -cam.ScreenToWorldPoint(new Vector3(0f, Screen.safeArea.yMin, 0f)).y;     // > 0: how far down the usable screen reaches
                float lowest = Mathf.Min(yMax, safeBottom - trayHalfHeight - trayOffset - 0.1f);
                if (lowest >= yMin) y = lowest;
            }
            Vector3 home = new Vector3(0f, -y, 0f);

            // Friends sharing one phone: every player gets the dice at THEIR OWN corner (next to their badge), so nobody has to
            // reach across the table. If a seat's own spot does not fit on this screen it uses the bottom-centre spot as before.
            Vector3 target = home;
            if (SeatDice && diceSeat >= 0 && TrySeatDiceSpot(diceSeat, out Vector3 spot)) target = spot;
            Vector3 now = dice.transform.position;
            if (!dicePlaced || (target - now).sqrMagnitude < 0.0004f) now = target;
            else now = Vector3.Lerp(now, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));   // a quick glide to the next player
            dicePlaced = true;
            dice.transform.position = now;
            UpdatePads(now);
            if (link != null) PlaceOnlineExtras(corners, now);
        }

        // ---------- the chat message bar (online) ----------

        RectTransform chatPreview;                                  // the box that shows a new chat message for a few seconds
        Vector2 chatPreviewHome;
        static readonly Vector3[] previewCorners = new Vector3[4];

        float cornerRowY;                                           // centre height of the bottom row of dice slots (set by AllSeatSpotsFit)
        const float chatBarHeight = 2.4f;                           // world units, a little more than the bar itself

        /// <summary>
        /// Online only: where the Auto button and the chat message bar go. With a slot in every corner they use the free bottom strip:
        /// the Auto button sits between the two bottom dice slots, and a new chat message shows just under that row. Without corner
        /// slots (a wide tablet) the single dice is at the bottom and the bar sits just above it, with Auto beside the dice.
        /// Either way nothing covers the dice, the badges, the mic, the chat button or the turn banner.
        /// </summary>
        void PlaceOnlineExtras(bool corners, Vector3 dicePosition)
        {
            float safeBottom = -cam.ScreenToWorldPoint(new Vector3(0f, Screen.safeArea.yMin, 0f)).y;
            bool autoBesideSlot = corners && PlaceAutoBesideOwnSlot();      // the Auto button belongs to MY slot, right next to my dice
            float barBottomInCorners = cornerRowY - padHalf - 0.2f - chatBarHeight;
            if (corners && barBottomInCorners >= -safeBottom + 0.05f)
            {
                if (!autoBesideSlot) hud.AlignAutoButton(new Vector2(0f, cornerRowY), true);
                PlaceChatPreview(cornerRowY - padHalf - 0.2f, true);
            }
            else
            {
                hud.AlignAutoButton(new Vector2(0f, dicePosition.y), false);
                PlaceChatPreview(dicePosition.y + padHalf + 0.25f, false);
            }
        }

        /// <summary>Put the Auto button on the screen-centre side of this player's own dice slot, in their seat colour. False (and the
        /// button is left for the caller to place) when there is no room there: off screen, on the mic/chat/pause/banner, or on another slot.</summary>
        bool PlaceAutoBesideOwnSlot()
        {
            int seat = MySeat();
            if (seat < 0 || !TrySeatDiceSpot(seat, out Vector3 spot)) return false;
            hud.SetAutoSeatColor(SeatStyle.Colors[seat]);
            hud.AlignAutoButton(new Vector2(spot.x, spot.y), true);
            if (!hud.TryGetAutoRect(out Rect autoRect)) return false;
            float towardCentre = spot.x > 0f ? -1f : 1f;
            hud.AlignAutoButton(new Vector2(spot.x + towardCentre * (padHalf + 0.25f + autoRect.width * 0.5f), spot.y), true);
            hud.TryGetAutoRect(out autoRect);
            float halfWidth = cam.orthographicSize * cam.aspect;
            if (autoRect.xMin < -halfWidth + 0.1f || autoRect.xMax > halfWidth - 0.1f) return false;
            hud.GetKeepClearRects(keepClear);
            if (Hits(autoRect, keepClear)) return false;
            for (int p = 0; p < playerCount; p++)
            {
                int other = game.State.SeatOf(p);
                if (other == seat || !TrySeatDiceSpot(other, out Vector3 otherSpot)) continue;
                if (autoRect.Overlaps(new Rect(otherSpot.x - padHalf, otherSpot.y - padHalf, padHalf * 2f, padHalf * 2f))) return false;
            }
            return true;
        }

        /// <summary>Does every player's own corner slot fit on this screen? Also finds the height of the bottom row of slots.</summary>
        bool AllSeatSpotsFit()
        {
            float bottomY = 0f; int bottoms = 0;
            for (int p = 0; p < playerCount; p++)
            {
                int seat = game.State.SeatOf(p);
                if (!TrySeatDiceSpot(seat, out Vector3 spot)) return false;
                if (seat > (int)Seat.Green) { bottomY += spot.y; bottoms++; }
            }
            if (bottoms == 0) return false;
            cornerRowY = bottomY / bottoms;
            return true;
        }

        /// <summary>Put the chat message bar with its top edge at 'edgeY' (below = true) or its bottom edge at 'edgeY' (below = false).</summary>
        void PlaceChatPreview(float edgeY, bool below)
        {
            if (chatPreview == null)
            {
                var canvasObject = GameObject.Find("HudCanvas");
                var found = canvasObject != null ? canvasObject.transform.Find("SafeArea/ChatPreview") : null;
                if (found == null) return;
                chatPreview = (RectTransform)found;
                chatPreviewHome = chatPreview.anchoredPosition;
            }
            if (!chatPreview.gameObject.activeInHierarchy) return;
            chatPreview.anchoredPosition = chatPreviewHome;                  // measure from the designed spot, then shift
            chatPreview.GetWorldCorners(previewCorners);
            float height = previewCorners[1].y - previewCorners[0].y;
            float wantedBottom = below ? edgeY - height : edgeY;
            chatPreview.position += new Vector3(0f, wantedBottom - previewCorners[0].y, 0f);
        }

        // ---------- every player's dice slot, always visible (pass & play) ----------

        const float padHalf = 1.25f;
        readonly GameObject[] pads = new GameObject[4];             // index = board seat; the slot of a player who is not rolling now
        SpriteRenderer trayRim;                                     // the rim of the tray that follows the dice (the active slot)
        Color trayRimDefault;
        bool trayRimCaptured;
        Transform arrow;                                            // points at the slot whose turn it is
        SpriteRenderer arrowRenderer;

        /// <summary>Build (or remove) the slots for this match. A slot is a copy of the dice tray, dimmed, with its player's colour on
        /// the rim, so everybody can see where their own dice will be; the real tray (with the dice in it) sits in the active slot.</summary>
        void SetupPads()
        {
            for (int i = 0; i < pads.Length; i++) { if (pads[i] != null) Destroy(pads[i]); pads[i] = null; }
            if (arrow != null) { Destroy(arrow.gameObject); arrow = null; arrowRenderer = null; }

            var tray = GameObject.Find("DiceTray");
            if (tray == null) return;
            if (trayRim == null)
                foreach (var sr in tray.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.name == "Rim") trayRim = sr;
            if (trayRim != null && !trayRimCaptured) { trayRimDefault = trayRim.color; trayRimCaptured = true; }
            if (trayRim != null && !SeatDice) trayRim.color = trayRimDefault;
            if (!SeatDice) return;

            for (int p = 0; p < playerCount; p++)
            {
                int seat = game.State.SeatOf(p);
                var pad = Instantiate(tray, tray.transform.parent);
                pad.name = "DicePad_" + SeatStyle.Names[seat];
                var follow = pad.GetComponent<FollowPosition>();
                if (follow != null) Destroy(follow);
                foreach (var sr in pad.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var c = sr.name == "Rim" ? Color.Lerp(SeatStyle.Colors[seat], Color.white, 0.1f) : sr.color;
                    sr.color = new Color(c.r, c.g, c.b, c.a * 0.8f);
                }
                pad.SetActive(false);
                pads[seat] = pad;
            }

            var go = new GameObject("DiceArrow");
            arrowRenderer = go.AddComponent<SpriteRenderer>();
            arrowRenderer.sprite = ArrowSprite();
            arrowRenderer.sortingOrder = 95;
            arrow = go.transform;
            go.SetActive(false);
        }

        /// <summary>Each frame: show the slots of the players who are not rolling, colour the active one, and point the arrow at it.</summary>
        void UpdatePads(Vector3 dicePosition)
        {
            if (!SeatDice) return;
            for (int seat = 0; seat < pads.Length; seat++)
            {
                if (pads[seat] == null) continue;
                bool show = seat != diceSeat && TrySeatDiceSpot(seat, out Vector3 spot);
                if (show) { TrySeatDiceSpot(seat, out spot); pads[seat].transform.position = spot; }
                if (pads[seat].activeSelf != show) pads[seat].SetActive(show);
            }
            if (trayRim != null && diceSeat >= 0) trayRim.color = Color.Lerp(SeatStyle.Colors[diceSeat], Color.white, 0.1f);

            if (arrow == null) return;
            bool pointing = diceSeat >= 0 && State == FlowState.WaitingForRoll && IsLocalHuman(game.CurrentPlayer);
            float side = 0f;
            if (pointing)
            {
                // beside the slot, on whichever side is free: the board's side first, else the screen-edge side
                float halfWidth = cam.orthographicSize * cam.aspect;
                bool hasAuto = hud.TryGetAutoRect(out Rect autoRect);
                hud.GetKeepClearRects(keepClear);
                float towardCentre = dicePosition.x > 0f ? -1f : 1f;
                foreach (float candidate in new[] { towardCentre, -towardCentre })
                {
                    float x = dicePosition.x + candidate * (padHalf + 1.0f);
                    var box = new Rect(x - 0.6f, dicePosition.y - 0.6f, 1.2f, 1.2f);
                    if (Mathf.Abs(x) + 0.6f > halfWidth || Hits(box, keepClear) || (hasAuto && box.Overlaps(autoRect))) continue;
                    side = candidate;
                    break;
                }
            }
            bool arrowShown = side != 0f;
            if (arrow.gameObject.activeSelf != arrowShown) arrow.gameObject.SetActive(arrowShown);
            if (!arrowShown) return;
            float bob = Mathf.Sin(Time.unscaledTime * 7f) * 0.18f;
            arrow.position = dicePosition + new Vector3(side * (padHalf + 1.0f + bob), 0f, 0f);
            arrow.localScale = new Vector3(-side, 1f, 1f);          // the sprite points right; flipped it points left
            arrowRenderer.color = Color.Lerp(SeatStyle.Colors[diceSeat], Color.white, 0.15f);
        }

        static Sprite arrowSprite;

        /// <summary>A plain triangle pointing right, drawn in code so no art file is needed.</summary>
        static Sprite ArrowSprite()
        {
            if (arrowSprite != null) return arrowSprite;
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float along = (x - 10f) / 76f;                          // 0 at the flat back, 1 at the tip
                    float reach = (1f - along) * 40f;                       // how far from the middle line the triangle still reaches
                    float edge = reach - Mathf.Abs(y - 48f);
                    byte a = along < 0f || along > 1f ? (byte)0 : (byte)(Mathf.Clamp01(edge) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            arrowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 80f);
            return arrowSprite;
        }

        // ---------- one dice position per player (offline, more than one person on this phone) ----------

        int diceSeat = -1;                                          // whose corner the dice sits in (a board seat)
        bool dicePlaced;                                            // the dice has been put somewhere since the match began

        /// <summary>Every match (online, against the computer, or friends sharing the phone): each player has a dice slot beside their
        /// own badge, and the dice moves to the corner of the player whose turn it is. When a seat's slot does not fit on a
        /// very wide screen, that seat falls back to the single dice at the bottom (see LateUpdate).</summary>
        bool SeatDice => true;

        /// <summary>The spot for a seat's dice: above its badge for the two top seats, below it for the two bottom seats,
        /// in line with the badge. False when that spot would run off the screen or touch the turn banner.</summary>
        readonly List<Rect> keepClear = new List<Rect>();

        static bool Hits(Rect tray, List<Rect> others)
        {
            foreach (var r in others) if (tray.Overlaps(r)) return true;
            return false;
        }

        bool TrySeatDiceSpot(int seat, out Vector3 spot)
        {
            spot = default;
            if (cam == null || !hud.TryGetBadgeRect(seat, out Rect badge)) return false;
            const float half = 1.25f, gap = 0.3f;                    // half the tray's size; space between badge and tray
            float screenHalfWidth = cam.orthographicSize * cam.aspect;
            bool top = seat <= (int)Seat.Green;                     // Red and Green sit above the board
            float x = Mathf.Clamp(badge.center.x, -screenHalfWidth + half + 0.2f, screenHalfWidth - half - 0.2f);
            float y = top ? badge.yMax + gap + half : badge.yMin - gap - half;
            var tray = new Rect(x - half, y - half, half * 2f, half * 2f);
            if (tray.yMax > cam.orthographicSize - 0.1f || tray.yMin < -cam.orthographicSize + 0.1f) return false;
            // the turn banner, pause button and (online) mic and chat buttons must stay clear: slide the slot towards the middle until
            // it is off them, but never so far that it meets the slot of the player next to it
            hud.GetKeepClearRects(keepClear);
            for (int guard = 0; guard < 40 && Hits(tray, keepClear); guard++)
            {
                x -= Mathf.Sign(x) * 0.2f;
                tray.x = x - half;
            }
            if (Hits(tray, keepClear) || Mathf.Abs(x) < half + 0.4f) return false;
            spot = new Vector3(x, y, 0f);
            return true;
        }

        /// <summary>Online: hand a player who left over to the computer, on every phone at the same moment.</summary>
        void Update()
        {
            if (link == null || game == null) return;
            if (link.IsHost)
                while (link.TryTakeDeparted(out int gone))
                    if (gone < slots.Length && !slots[gone].isAi) { Log("seat " + gone + " disconnected"); link.BroadcastTakeover(gone); }
            while (link.TryTakeTakeover(out int seatIndex)) ApplyTakeover(seatIndex);
            if (!link.IsHost && link.TryTakeResync(out var story)) ResumeFromSnapshot(story);
            CheckLastPersonStanding();
            ShowAwayStatus();
        }

        /// <summary>
        /// This phone was away for a while and the host sent the whole story of the match (every dice value and pick, and who the
        /// computer took over). Play it again instantly on a fresh engine, put the pawns where they belong and carry on from the
        /// turn the others are in. The rules engine is deterministic, so every phone ends up in exactly the same position.
        /// </summary>
        void ResumeFromSnapshot(MatchResync story)
        {
            Log("resync: " + story.history.Length + " events, " + story.takenOver.Length + " computer seats");
            StopAllCoroutines();
            StopTimer();
            queuedDice = new QueuedDiceSource();
            game = new LudoGame(Board.DefaultSeats(playerCount), Rules, queuedDice);
            RollResult waiting = MatchReplay.Apply(game, queuedDice, story.history);    // a roll whose pick has not been announced yet, or null
            board.Bind(game);                                            // pawns straight to where the story ended
            foreach (int gone in story.takenOver) ApplyTakeover(gone);
            if (leftForInactivity) return;                               // my own seat was given away while I was gone
            hud.SetStatus("");
            StartCoroutine(Flow(waiting, opening: false));
        }

        void ApplyTakeover(int player)
        {
            if (player < 0 || player >= slots.Length || slots[player].isAi) return;
            bool wasMe = IsLocalHuman(player);                    // the host took MY seat because I was away too long
            string name = GameSession.NameOf(slots[player], player);
            string country = slots[player].onlineCountry;
            slots[player] = PlayerSlot.OnlineCpu(name, AiDifficulty.Easy);
            slots[player].onlineCountry = country;                // the seat still shows whose place it was
            ais[player] = AiFactory.Create(AiDifficulty.Easy, Environment.TickCount + player);
            hud.SetBadge(game.State.SeatOf(player), name, GameSession.SubtitleOf(slots[player], game.State.SeatOf(player), MySeat()), GameSession.AvatarOf(slots[player], player), GameSession.FlagOf(slots[player]));
            Log("player " + player + " left: the computer plays for them");

            if (wasMe && !leftForInactivity)
            {
                leftForInactivity = true;
                StopAllCoroutines();
                StopTimer();
                var forfeit = GameSession.Settler?.Left(ResultKind.Forfeit);      // a ranked match left early is a loss (the summary shows what it cost)
                GameSession.LeaveOnline?.Invoke();
                GameSession.ClearOnline();
                State = FlowState.GameOver;
                hud.ShowEnded("You were away too long", forfeit);
            }
        }

        // ---------- the turn, start to finish ----------

        IEnumerator Flow(RollResult resume = null, bool opening = true)
        {
            yield return null;   // let one frame pass so the layout (dice position) is ready before the first turn
            if (opening)
            {
                // clear whatever the last match left on screen first: a restart would otherwise count down over the old
                // player's banner and a dice still tumbling from the game that was just abandoned
                dice.ShowSeatIcon(game.State.SeatOf(game.CurrentPlayer));
                hud.SetTurnSeat(game.State.SeatOf(game.CurrentPlayer));
                yield return hud.PlayCountdown();            // 3 - 2 - 1 - GO! (never after a reconnect: the others are already playing)
            }
            while (game.Phase != TurnPhase.GameOver)
            {
                if (link != null && !link.IsConnected) { yield return ConnectionLost(); yield break; }

                int player = game.CurrentPlayer;
                int seat = game.State.SeatOf(player);
                hud.SetTurnSeat(seat);
                diceSeat = seat;                             // pass & play: the dice glides to this player's corner
                dice.ApplySkin(slots[player].diceSkin);     // online: whoever throws uses their own design

                RollResult roll = resume;                    // after a reconnect the turn may already be rolled: go straight to choosing
                resume = null;
                if (roll != null) dice.Show(roll.Value);      // already rolled (a reconnect): show it, ready to choose a pawn
                else dice.ShowSeatIcon(seat);                 // waiting for this player's roll: their own colour, not a leftover number
                Log("turn " + SeatStyle.Names[seat] + (IsLocalHuman(player) ? " human" : slots[player].isAi ? " cpu" : " remote"));

                if (roll == null)
                {
                // 1. a person taps the dice; the computer just waits a moment and rolls; a remote player's phone tells the host
                State = FlowState.WaitingForRoll;
                rolledValue = 0;
                yield return ObtainRoll(player, seat);
                StopTimer();
                if (leftForInactivity) yield break;
                if (link != null && rolledValue == 0) { yield return ConnectionLost(); yield break; }
                if (link != null && rolledValue == MatchReplay.SkipEvent)        // out of time: no roll, no move, the dice goes on
                {
                    yield return PassOnAfterTimeout(player);
                    continue;
                }

                // 2. roll: the engine decides the value NOW, the dice only plays the animation
                State = FlowState.Rolling;
                if (queuedDice != null) queuedDice.Push(rolledValue);
                roll = game.Roll();
                Log(SeatStyle.Names[seat] + " rolled " + roll.Value + " pass=" + roll.Pass + " legal=" + roll.LegalMoves.Length + (roll.RollAgain ? " again" : ""));
                yield return dice.PlayRoll(roll.Value, seat);        // thrown from the roller's corner; shows the engine's value
                hud.ShowPendingRolls(game.PendingRolls, seat);

                if (roll.Pass != PassReason.None)
                {
                    hud.ShowPendingRolls(null, seat);
                    hud.ShowToast(roll.Pass == PassReason.ThreeSixes ? ToastKind.ThreeSixes : ToastKind.NoMoves, 1.3f);
                    yield return new WaitForSeconds(GameSession.Beat(1.3f));
                    continue;   // the engine already passed the turn
                }
                if (roll.RollAgain)                                  // Ludo Star rule: a six - roll again before any pawn moves
                {
                    hud.ShowToast(ToastKind.RollAgain, 0.9f);
                    yield return new WaitForSeconds(GameSession.Beat(0.5f));
                    continue;
                }
                }

                // 3 + 4. one move for every number rolled (Ludo Star rule: 6 then 5 = two moves, in the order the player picks)
                while (game.Phase == TurnPhase.WaitingForMove && game.CurrentPlayer == player)
                {
                    var options = roll ?? Snapshot();
                    roll = null;

                    // choose a token (or auto-play when every option is the same move)
                    Move chosen;
                    if (OnlyOneChoice(options))
                    {
                        chosen = options.LegalMoves[0];
                        yield return new WaitForSeconds(GameSession.Beat(0.25f));
                    }
                    else if (link != null)
                    {
                        yield return ChooseOnline(player, seat, options);
                        StopTimer();
                        if (leftForInactivity) yield break;
                        if (pickSkipped) { yield return PassOnAfterTimeout(player); continue; }
                        if (link != null && !link.IsConnected && chosenMove.Equals(default(Move))) { yield return ConnectionLost(); yield break; }
                        chosen = chosenMove;
                    }
                    else if (slots[player].isAi)
                    {
                        chosen = ais[player].Choose(game.State, Rules, options.LegalMoves);      // the AI may only pick from the engine's legal moves
                        var highlight = new[] { chosen };
                        board.SetSelectable(highlight, true);                             // show which pawn it picked
                        yield return new WaitForSeconds(GameSession.Beat(0.5f));
                        board.SetSelectable(highlight, false);
                    }
                    else
                    {
                        yield return PickByTap(options);
                        if (undoRequested) { TakeBackMove(); yield break; }   // TakeBackMove starts the flow again
                        chosen = chosenMove;
                    }

                    // remember the position before a person's own move, so they can take it back (offline only)
                    bool mine = UndoOffered && IsLocalHuman(player);
                    if (mine) undoPoint = game.Save();

                    // play the move and wait for the animation to finish
                    State = FlowState.Moving;
                    MoveResult result = game.Play(chosen);
                    Log("moved token " + chosen.Token + " " + chosen.From + "->" + chosen.To + " with " + chosen.Roll + " captured=" + result.Captured.Length + " extra=" + result.ExtraTurn + " more=" + result.MoreMoves);
                    hud.ShowPendingRolls(game.PendingRolls, seat);
                    if (result.Captured.Length > 0) hud.ShowToast(ToastKind.Captured, 1.1f);
                    yield return new WaitWhile(() => board.IsAnimating);

                    // the move is finished and the turn is about to pass: a short window to take it back
                    if (mine && !result.GameWon && !result.MoreMoves && undosLeft > 0)
                    {
                        yield return OfferUndo();
                        if (undoRequested) { TakeBackMove(); yield break; }   // TakeBackMove starts the flow again
                    }
                }
                hud.ShowPendingRolls(null, seat);
            }

            // game over: the result screen has its own buttons (Play Again / Main Menu)
            int winner = game.State.Winner;
            Log("game over, winner player " + winner);
            yield return FinishMatch(winner, false);
        }

        /// <summary>
        /// The match is over. Online, the settler decides what it is worth (Rank Points, XP, coins) and the result screen shows it.
        /// 'byForfeit': the winner is the last person left because everybody else stopped playing.
        /// </summary>
        IEnumerator FinishMatch(int winner, bool byForfeit)
        {
            if (ending) yield break;
            ending = true;
            State = FlowState.GameOver;
            StopTimer();
            hud.SetStatus("");
            PlayerSlot winnerSlot = winner < slots.Length ? slots[winner] : PlayerSlot.Human;
            int partner = TeamPartner(winner);
            bool teams = partner >= 0;
            int me = LocalPlayer();
            bool mineWon = me >= 0 && (me == winner || me == partner);
            // Team Up: everything below is decided per side, so each phone reports its OWN result (won if its team won)
            int settleWinner = teams && mineWon ? me : winner;
            bool winnerIsPerson = !winnerSlot.isAi || (teams && !slots[partner].isAi);
            MatchSummary summary = null;
            var settler = GameSession.Settler;
            if (settler != null)
            {
                var task = settler.Finished(settleWinner, winnerIsPerson, byForfeit);
                while (!task.IsCompleted) yield return null;
                if (!task.IsFaulted) summary = task.Result;
                else Log("settling the match failed: " + task.Exception?.GetBaseException().Message);
            }
            string title = teams
                ? GameSession.NameOf(winnerSlot, winner) + " + " + GameSession.NameOf(slots[partner], partner)
                : GameSession.NameOf(winnerSlot, winner);
            hud.ShowResult(title, SeatStyle.Colors[game.State.SeatOf(winner)], GameSession.AvatarOf(winnerSlot, winner), summary,
                teams ? mineWon : IsLocalHuman(winner), byForfeit, GameSession.FlagOf(winnerSlot));
        }

        /// <summary>The board seat of the person holding this phone (-1 in an offline pass-and-play game).</summary>
        int MySeat()
        {
            int me = LocalPlayer();
            return me >= 0 && game != null ? game.State.SeatOf(me) : -1;
        }

        /// <summary>The lowest-numbered seat still played by a person (-1 if none).</summary>
        int FirstHuman()
        {
            for (int p = 0; p < slots.Length && p < playerCount; p++) if (!slots[p].isAi) return p;
            return -1;
        }

        /// <summary>Team Up: the player sharing this player's side (-1 in every other mode).</summary>
        int TeamPartner(int player)
        {
            if (game == null || !game.State.Teams) return -1;
            for (int p = 0; p < game.State.PlayerCount; p++)
                if (p != player && game.State.SameTeam(p, player)) return p;
            return -1;
        }

        /// <summary>My own player number in an online match (-1 if none).</summary>
        int LocalPlayer()
        {
            for (int p = 0; p < slots.Length; p++) if (IsLocalHuman(p)) return p;
            return -1;
        }

        /// <summary>
        /// Online: when only one person is left at the table (everybody else forfeited: they did not come back in time or left), that
        /// person wins by forfeit at once. Every phone works this out the same way from the same takeovers.
        /// </summary>
        void CheckLastPersonStanding()
        {
            if (ending || State == FlowState.GameOver || leftForInactivity || GameSession.Settler == null) return;
            int humans = 0, last = -1;
            for (int p = 0; p < slots.Length && p < playerCount; p++)
                if (!slots[p].isAi) { humans++; last = p; }
            // Team Up: two people still playing are only "the last one standing" if they are partners
            if (humans == 2 && game != null && game.State.Teams && game.State.SameTeam(FirstHuman(), last)) humans = 1;
            if (humans != 1) return;
            Log("only player " + last + " is left: wins by forfeit");
            StopAllCoroutines();
            StartCoroutine(FinishMatch(last, true));
        }

        /// <summary>The banner line "Ali lost connection - waiting 12s" while somebody else's phone is away.</summary>
        void ShowAwayStatus()
        {
            if (State == FlowState.GameOver || ending || leftForInactivity || !link.IsConnected || link.IsReconnecting) return;
            string text = "";
            for (int p = 0; p < slots.Length && p < playerCount; p++)
            {
                if (slots[p].isAi || IsLocalHuman(p)) continue;
                float left = link.AwaySecondsLeft(p);
                if (left > 0f) { text = GameSession.NameOf(slots[p], p) + " lost connection - waiting " + Mathf.CeilToInt(left) + "s"; break; }
            }
            if (text == awayShown) return;
            awayShown = text;
            hud.SetStatus(text);
        }

        /// <summary>This phone's own person plays this player (offline: any human seat; online: only my seat).</summary>
        bool IsLocalHuman(int player) => !slots[player].isAi && !slots[player].isRemote;

        // ---------- undo (offline only) ----------

        /// <summary>Can a takeback be offered at all in this match? (see StartGame for why online is excluded)</summary>
        bool UndoOffered => false;                      // takebacks were removed; Auto play replaced the button

        /// <summary>
        /// Hold the Undo button up for a moment after a person's move has ended their turn. Waiting is the whole point:
        /// once the next player rolls there is nothing sensible left to take back, so the offer has to be made now.
        /// </summary>
        IEnumerator OfferUndo()
        {
            undoRequested = false;
            for (float t = 0f; t < undoWindowSeconds; t += Time.deltaTime)
            {
                if (false) { undoRequested = true; break; }
                yield return null;
            }
        }

        /// <summary>
        /// Put the position back to just before the person's last move and start the turn again from there. The engine
        /// restores the pawns, the numbers still to play, the six counter and Master mode's capture flag together
        /// (LudoGame.Restore), so nothing can be left half-undone; the dice keeps the number it rolled, so a takeback can
        /// never be used to fish for a six.
        /// </summary>
        void TakeBackMove()
        {
            undoRequested = false;
            if (undoPoint == null) { StartCoroutine(Flow(null, opening: false)); return; }
            undosLeft = Mathf.Max(0, undosLeft - 1);
            game.Restore(undoPoint);
            undoPoint = null;
            board.Bind(game);                                   // pawns straight back to where they stood
            dice.Show(game.LastRoll);
            int seat = game.State.SeatOf(game.CurrentPlayer);
            hud.SetTurnSeat(seat);
            hud.ShowPendingRolls(game.PendingRolls, seat);
            Log("undo: back to player " + game.CurrentPlayer + ", " + undosLeft + " left");
            StartCoroutine(Flow(Snapshot(), opening: false));   // straight back to choosing a pawn, no new roll
        }

        // ---------- rolling ----------

        /// <summary>
        /// Offline: only waits (for the tap, or the computer's thinking time). Online: also settles the dice value in
        /// rolledValue - the host draws it and announces it, everybody (host too) then reads the announcement.
        /// rolledValue stays 0 if the connection died.
        /// </summary>
        IEnumerator ObtainRoll(int player, int seat)
        {
            bool online = link != null;
            bool host = !online || link.IsHost;
            StartTimer(online && !slots[player].isAi);            // online, a person has limited time to roll
            bool skip = false;                                    // out of time: nothing is rolled, the turn passes on

            if (IsLocalHuman(player))
            {
                dice.SetReady(true);
                if (online && GameSettings.Vibration) Haptics.Pulse(40);        // a nudge: it is your turn
                Vector3 diceScreen = cam.WorldToScreenPoint(dice.transform.position);
                Log("awaiting dice tap@" + (int)diceScreen.x + "," + (int)(Screen.height - diceScreen.y));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (AutoPlay) { yield return new WaitForSeconds(GameSession.Beat(0.6f)); waitTimedOut = false; }
                else
#endif
                yield return WaitForDiceTap(tapRadius * (SeatDice ? 2.3f : 1.6f));   // pass & play: a big target, people tap in a hurry
                if (online) NoteOwnTurn(player, waitTimedOut);
                if (leftForInactivity) yield break;
                skip = online && waitTimedOut;
                if (online && !host && !skip) link.SendRollRequest();       // out of time: no request, the host passes the turn on
            }
            else if (slots[player].isAi)
            {
                dice.SetReady(false);
                if (host) yield return new WaitForSeconds(GameSession.Beat(aiThinkSeconds));
            }
            else
            {
                dice.SetReady(false);
                if (host)
                {
                    yield return WaitForRemote(player, () => link.TryTakeRollRequest(player));
                    skip = waitTimedOut;
                    NoteRemoteTurn(player, waitTimedOut);
                }
            }

            if (!online) yield break;

            if (host) link.BroadcastRoll(skip ? MatchReplay.SkipEvent : hostDice.Next());
            while (!link.TryTakeRoll(out rolledValue))
            {
                if (!link.IsConnected) { rolledValue = 0; yield break; }
                TickTimer();
                yield return null;
            }
        }

        /// <summary>Wait for a tap on the dice. Online it gives up when the time limit is over (waitTimedOut = true).</summary>
        IEnumerator WaitForDiceTap(float radius)
        {
            waitTimedOut = false;
            while (true)
            {
                if (hud.AutoOn)                                         // auto play: roll for the player after a short beat
                {
                    yield return new WaitForSeconds(GameSession.Beat(0.8f));
                    waitTimedOut = false;
                    yield break;
                }
                if (timerActive)
                {
                    TickTimer();
                    if (Time.time - timerStart >= TurnSeconds) { waitTimedOut = true; yield break; }
                }
                if (Input.GetMouseButtonDown(0) && TapReachesBoard())
                {
                    Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
                    world.z = 0f;
                    lastTap = world;
                    Log("tap at " + world + " state=" + State + " frame=" + Time.frameCount);
                    if (Vector2.Distance(lastTap, dice.transform.position) <= radius) { yield return null; yield break; }
                }
                yield return null;
            }
        }

        // ---------- choosing ----------

        /// <summary>Offline human: wait until a legal pawn is tapped.</summary>
        IEnumerator PickByTap(RollResult roll)
        {
            State = FlowState.SelectingToken;
            board.SetSelectable(roll.LegalMoves, true);
            // a Ludo Star turn can be several moves: while the person is picking the next number, the previous move of
            // this same turn can still be taken back
            bool canUndoNow = UndoOffered && undosLeft > 0 && undoPoint != null;
            LogSelectable(roll);
            Move picked;
            bool asking = false;                                        // the "which number?" pop-up is open
            int askedToken = -1;
            waitTimedOut = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (AutoPlay)
            {
                yield return new WaitForSeconds(GameSession.Beat(0.6f));
                picked = AiFor(game.CurrentPlayer).Choose(game.State, Rules, roll.LegalMoves);
                board.SetSelectable(roll.LegalMoves, false);
                chosenMove = picked;
                yield break;
            }
#endif
            while (true)
            {
                if (hud.AutoOn)                                         // auto play: the best-looking simple move
                {
                    yield return new WaitForSeconds(GameSession.Beat(0.8f));
                    picked = AiFor(game.CurrentPlayer).Choose(game.State, Rules, roll.LegalMoves);
                    waitTimedOut = false;
                    break;
                }
                if (timerActive)
                {
                    TickTimer();
                    if (Time.time - timerStart >= TurnSeconds)
                    {
                        // out of time: nothing is moved, the turn passes on (ChooseOnline announces it)
                        waitTimedOut = true;
                        picked = default;
                        break;
                    }
                }
                if (canUndoNow && false)                       // take back the previous move of this same turn
                {
                    undoRequested = true;
                    picked = default;
                    break;
                }
                int value = hud.TakeChosenValue();
                if (value > 0 && asking)                                // the number was chosen in the pop-up
                {
                    picked = MoveFor(roll, askedToken, value);
                    break;
                }
                if (Input.GetMouseButtonDown(0) && TapReachesBoard())
                {
                    Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
                    world.z = 0f;
                    lastTap = world;
                    Log("tap at " + world + " state=" + State + " frame=" + Time.frameCount);
                    hud.HideValueChoice();
                    asking = false;
                    if (board.TryPick(lastTap, roll.LegalMoves, tapRadius, out picked))
                    {
                        // Ludo Star rule: this pawn can move with more than one of the rolled numbers - ask which one first
                        var values = ValuesFor(roll, picked.Token);
                        if (values.Count <= 1) break;
                        asking = true;
                        askedToken = picked.Token;
                        hud.AskValue(board.TokenWorldPosition(picked.Player, picked.Token), values);
                    }
                    yield return null;
                }
                yield return null;
            }
            hud.HideValueChoice();
            board.SetSelectable(roll.LegalMoves, false);
            chosenMove = picked;
        }

        /// <summary>The different numbers this token can move with (in the order they were rolled).</summary>
        static List<int> ValuesFor(RollResult roll, int token)
        {
            var values = new List<int>(3);
            foreach (var m in roll.LegalMoves)
                if (m.Token == token && !values.Contains(m.Roll)) values.Add(m.Roll);
            return values;
        }

        static Move MoveFor(RollResult roll, int token, int value)
        {
            foreach (var m in roll.LegalMoves)
                if (m.Token == token && m.Roll == value) return m;
            return roll.LegalMoves[0];
        }

        bool pickSkipped;                                           // the pick that was just announced is "ran out of time"

        /// <summary>The player ran out of time (announced to everybody): nothing is rolled or moved, the dice goes to the next player.</summary>
        IEnumerator PassOnAfterTimeout(int player)
        {
            Log("player " + player + " ran out of time: the turn passes on");
            pickSkipped = false;
            board.SetSelectable(new Move[0], false);
            hud.ShowPendingRolls(null, game.State.SeatOf(player));
            hud.ShowToast(ToastKind.TimeUp, 1.2f);
            game.SkipTurn();
            yield return new WaitForSeconds(GameSession.Beat(0.9f));
        }

        /// <summary>Online: whoever owns the seat decides, the host checks and announces "move N", everybody plays move N.</summary>
        IEnumerator ChooseOnline(int player, int seat, RollResult roll)
        {
            bool host = link.IsHost;
            bool outOfTime = false;
            chosenMove = default;
            pickSkipped = false;
            StartTimer(!slots[player].isAi);                     // a person has limited time to pick a pawn

            if (IsLocalHuman(player))
            {
                yield return PickByTap(roll);
                outOfTime = waitTimedOut;
                NoteOwnTurn(player, waitTimedOut);
                if (leftForInactivity) yield break;
                int index = outOfTime ? -1 : IndexOf(roll, chosenMove);      // -1: out of time, the turn passes on
                if (host) link.BroadcastPick(index);
                else link.SendPickRequest(index);
            }
            else if (host)
            {
                if (slots[player].isAi)
                {
                    yield return new WaitForSeconds(GameSession.Beat(0.3f));
                }
                else
                {
                    requestedPick = -1;
                    bool arrived = false;
                    yield return WaitForRemote(player, () => arrived = link.TryTakePickRequest(player, out requestedPick));
                    outOfTime = waitTimedOut || (arrived && requestedPick < 0);   // too slow, or their phone says it ran out of time
                    NoteRemoteTurn(player, outOfTime);
                }
                int index = outOfTime &&!slots[player].isAi
                    ? -1
                    : requestedPick >= 0 && requestedPick < roll.LegalMoves.Length && !slots[player].isAi
                        ? requestedPick
                        : IndexOf(roll, AiFor(player).Choose(game.State, Rules, roll.LegalMoves));   // computer, or a player who dropped
                link.BroadcastPick(index);
                requestedPick = -1;
            }

            // every phone (the host too) now reads the announced move
            int announced;
            while (!link.TryTakePick(out announced))
            {
                if (!link.IsConnected) { chosenMove = default; yield break; }
                TickTimer();
                yield return null;
            }
            if (announced < 0) { pickSkipped = true; yield break; }          // out of time: no move, the turn passes on
            if (announced >= roll.LegalMoves.Length) announced = 0;
            Move chosen = roll.LegalMoves[announced];

            if (!IsLocalHuman(player))
            {
                var highlight = new[] { chosen };
                board.SetSelectable(highlight, true);                // show which pawn was picked
                yield return new WaitForSeconds(GameSession.Beat(0.5f));
                board.SetSelectable(highlight, false);
            }
            chosenMove = chosen;
        }

        IAiPlayer AiFor(int player)
        {
            if (ais[player] == null) ais[player] = AiFactory.Create(AiDifficulty.Easy, Environment.TickCount + player);
            return ais[player];
        }

        static int IndexOf(RollResult roll, Move move)
        {
            for (int i = 0; i < roll.LegalMoves.Length; i++)
                if (roll.LegalMoves[i].Equals(move)) return i;
            return 0;
        }

        /// <summary>Host: wait for a remote player's request. Ends early when they leave, the connection dies or they take too long.</summary>
        IEnumerator WaitForRemote(int player, Func<bool> requestArrived)
        {
            float waited = 0f;
            waitTimedOut = false;
            while (true)
            {
                if (slots[player].isAi) yield break;                 // they left: the computer plays their turn
                if (requestArrived()) yield break;
                if (link.IsAway(player)) { waitTimedOut = false; yield break; }     // dropped a moment ago: play for them, keep their seat
                waited += Time.deltaTime;
                TickTimer();
                if (waited >= TurnSeconds + hostGraceSeconds) { waitTimedOut = true; yield break; }   // too slow: play for them this once
                yield return null;
            }
        }

        // ---------- the time limit for a turn (online) ----------

        void StartTimer(bool active)
        {
            timerStart = Time.time;
            timerActive = active && link != null;
            if (!timerActive) hud.HideTimer();
        }

        void TickTimer()
        {
            if (timerActive) hud.ShowTimer(Mathf.Max(0f, TurnSeconds - (Time.time - timerStart)), TurnSeconds);
        }

        void StopTimer()
        {
            timerActive = false;
            hud.HideTimer();
        }

        /// <summary>My own turn ended (I acted, or time ran out). Too many idle turns in a row and I leave, so the game is not held up.</summary>
        void NoteOwnTurn(int player, bool timedOut)
        {
            if (!timedOut) { missedTurns[player] = 0; return; }
            missedTurns[player]++;
            Log("player " + player + " ran out of time (" + missedTurns[player] + " in a row)");
            if (missedTurns[player] >= missesBeforeKick && link != null && !link.IsHost)
            {
                leftForInactivity = true;
                StopTimer();
                var forfeit = GameSession.Settler?.Left(ResultKind.Forfeit);
                GameSession.LeaveOnline?.Invoke();
                GameSession.ClearOnline();
                State = FlowState.GameOver;
                hud.ShowEnded("You were away too long", forfeit);
            }
        }

        /// <summary>Host: a remote player's turn ended. If their phone stays silent turn after turn, the computer takes the seat.</summary>
        void NoteRemoteTurn(int player, bool timedOut)
        {
            if (!timedOut) { missedTurns[player] = 0; return; }
            missedTurns[player]++;
            Log("player " + player + " did not answer in time (" + missedTurns[player] + " in a row)");
            if (missedTurns[player] >= missesBeforeKick && !slots[player].isAi) link.BroadcastTakeover(player);
        }

        /// <summary>Is the person in this board seat talking right now? (online voice chat; the HUD shows a glow)</summary>
        public bool SeatIsSpeaking(int seat)
        {
            if (game == null || GameSession.SpeakingProbe == null) return false;
            for (int p = 0; p < slots.Length && p < playerCount; p++)
                if (game.State.SeatOf(p) == seat && !string.IsNullOrEmpty(slots[p].onlineId))
                    return GameSession.SpeakingProbe(slots[p].onlineId);
            return false;
        }

        IEnumerator ConnectionLost()
        {
            if (leftForInactivity) yield break;                  // we already told the player why they are out

            // a guest tries to get back in for a while; if it works, Update() receives the story of the match and restarts the flow
            if (link != null && !link.IsHost)
            {
                Log("connection lost: trying to reconnect");
                StopTimer();
                link.StartReconnect();
                float until = Time.unscaledTime + link.ReconnectSecondsLeft + 5f;
                while (Time.unscaledTime < until && (link.IsReconnecting || link.IsConnected))
                {
                    hud.SetStatus("Connection lost - reconnecting... " + Mathf.CeilToInt(link.ReconnectSecondsLeft) + "s");
                    yield return null;
                }
                if (link.HostGone && !ending && LocalPlayer() >= 0)      // the room is gone while my own connection works: the host left, I win by forfeit
                {
                    Log("the host left: win by forfeit");
                    yield return FinishMatch(LocalPlayer(), true);
                    yield break;
                }
            }
            State = FlowState.GameOver;
            Log("connection lost");
            var lost = GameSession.Settler?.Left(ResultKind.DisconnectLoss);       // did not get back in time: a disconnect loss
            hud.ShowEnded("Connection lost", lost);
            yield break;
        }

        /// <summary>Dev only: prints the screen position of every tappable token (for scripted phone tests).</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("LUDO_TRACE")]
        void LogSelectable(RollResult roll)
        {
            var sb = new System.Text.StringBuilder("select");
            foreach (var m in roll.LegalMoves)
            {
                Vector3 s = cam.WorldToScreenPoint(board.TokenWorldPosition(m.Player, m.Token));
                sb.Append(" token" + m.Token + "@" + (int)s.x + "," + (int)(Screen.height - s.y));
            }
            Log(sb.ToString());
        }

        static bool OnlyOneChoice(RollResult roll) => MatchReplay.OnlyOneChoice(roll);   // (several tokens in base = identical moves)

        /// <summary>The moves waiting right now (after the first of several rolled numbers was played).</summary>
        RollResult Snapshot()
        {
            var moves = new Move[game.LegalMoves.Count];
            for (int i = 0; i < moves.Length; i++) moves[i] = game.LegalMoves[i];
            return new RollResult(game.CurrentPlayer, game.LastRoll, moves, PassReason.None);
        }

        // ---------- taps (mouse in the Editor, first finger on a phone) ----------

        /// <summary>A tap counts for the board only when the game is not paused and the finger is not on a UI button.</summary>
        static bool TapReachesBoard()
        {
            if (Time.timeScale == 0f) return false;
            var events = EventSystem.current;
            if (events == null) return true;
            if (Input.touchCount > 0) return !events.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return !events.IsPointerOverGameObject();
        }

        IEnumerator WaitForTap()
        {
            while (!(Input.GetMouseButtonDown(0) && TapReachesBoard())) yield return null;
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            lastTap = world;
            Log("tap at " + world + " state=" + State + " frame=" + Time.frameCount);
            yield return null;   // never let one tap answer two questions
        }

        IEnumerator WaitForTapNear(Func<Vector3> centre, float radius)
        {
            while (true)
            {
                yield return WaitForTap();
                if (Vector2.Distance(lastTap, centre()) <= radius) yield break;
            }
        }
    }
}
