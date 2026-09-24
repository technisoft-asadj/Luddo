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
        const float hostGraceSeconds = 5f;                           // online host: waits a little longer than the player's own phone, so the phone's auto-play arrives first
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
            undosLeft = link == null ? Rules.UndosPerMatch : 0;
            undoPoint = null;
            undoRequested = false;
            hud.SetUndo(false, undosLeft);
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
            StartCoroutine(Flow());
        }

        /// <summary>Keep the dice's tray in the open space between the board and the bottom edge, whatever the phone's shape.
        /// A fixed spot (rather than one that hunts for room next to whichever badge is on top) can never collide with the
        /// turn banner or the mode line above the board; whose turn it is shows on the dice itself (see DiceView.ShowSeatIcon).</summary>
        void LateUpdate()
        {
            if (cam == null) return;
            float half = BoardGrid.Size * 0.5f;
            float middle = (half + 2f + cam.orthographicSize) * 0.5f;
            dice.transform.position = new Vector3(0f, -middle, 0f);
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
                hud.SetUndo(false, undosLeft);
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
        bool UndoOffered => link == null && Rules.UndoAllowed;

        /// <summary>
        /// Hold the Undo button up for a moment after a person's move has ended their turn. Waiting is the whole point:
        /// once the next player rolls there is nothing sensible left to take back, so the offer has to be made now.
        /// </summary>
        IEnumerator OfferUndo()
        {
            undoRequested = false;
            hud.SetUndo(true, undosLeft);
            for (float t = 0f; t < undoWindowSeconds; t += Time.deltaTime)
            {
                if (hud.TakeUndo()) { undoRequested = true; break; }
                yield return null;
            }
            hud.SetUndo(false, undosLeft);
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
            hud.SetUndo(false, undosLeft);
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

            if (IsLocalHuman(player))
            {
                dice.SetReady(true);
                Vector3 diceScreen = cam.WorldToScreenPoint(dice.transform.position);
                Log("awaiting dice tap@" + (int)diceScreen.x + "," + (int)(Screen.height - diceScreen.y));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (AutoPlay) { yield return new WaitForSeconds(GameSession.Beat(0.6f)); waitTimedOut = false; }
                else
#endif
                yield return WaitForDiceTap(tapRadius * 1.6f);
                if (online) NoteOwnTurn(player, waitTimedOut);
                if (leftForInactivity) yield break;
                if (online && !host) link.SendRollRequest();       // (a timed-out player's phone rolls for them)
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
                    NoteRemoteTurn(player, waitTimedOut);
                }
            }

            if (!online) yield break;

            if (host) link.BroadcastRoll(hostDice.Next());
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
            hud.SetUndo(canUndoNow, undosLeft);
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
                if (timerActive)
                {
                    TickTimer();
                    if (Time.time - timerStart >= TurnSeconds)
                    {
                        // out of time: the best-looking simple move is played for the player
                        waitTimedOut = true;
                        picked = AiFor(game.CurrentPlayer).Choose(game.State, Rules, roll.LegalMoves);
                        break;
                    }
                }
                if (canUndoNow && hud.TakeUndo())                       // take back the previous move of this same turn
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
            hud.SetUndo(false, undosLeft);
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

        /// <summary>Online: whoever owns the seat decides, the host checks and announces "move N", everybody plays move N.</summary>
        IEnumerator ChooseOnline(int player, int seat, RollResult roll)
        {
            bool host = link.IsHost;
            chosenMove = default;
            StartTimer(!slots[player].isAi);                     // a person has limited time to pick a pawn

            if (IsLocalHuman(player))
            {
                yield return PickByTap(roll);
                NoteOwnTurn(player, waitTimedOut);
                if (leftForInactivity) yield break;
                int index = IndexOf(roll, chosenMove);
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
                    yield return WaitForRemote(player, () => link.TryTakePickRequest(player, out requestedPick));
                    NoteRemoteTurn(player, waitTimedOut);
                }
                int index = requestedPick >= 0 && requestedPick < roll.LegalMoves.Length && !slots[player].isAi
                    ? requestedPick
                    : IndexOf(roll, AiFor(player).Choose(game.State, Rules, roll.LegalMoves));   // computer, or a player who took too long
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
            if (announced < 0 || announced >= roll.LegalMoves.Length) announced = 0;
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
