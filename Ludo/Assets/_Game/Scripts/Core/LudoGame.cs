using System;
using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>
    /// Runs one game: whose turn it is, rolling, playing a move, extra turns, passing, winning.
    /// It only decides WHAT happens. Animation, sound and UI listen to the events and decide how it LOOKS.
    ///
    /// Classic flow:  WaitingForRoll --Roll()--> WaitingForMove --Play(move)--> WaitingForRoll (same or next player)
    ///                (no legal move / three sixes: turn passes inside Roll())
    /// Ludo Star flow (RulesConfig.RollAgainOnSix): a 6 keeps the phase at WaitingForRoll (roll again, RollResult.RollAgain);
    ///                the first non-6 opens WaitingForMove with the moves of EVERY number rolled; each Play uses one number
    ///                (Move.Roll) and stays in WaitingForMove while numbers are left (MoveResult.MoreMoves). A capture or reaching
    ///                home earns one more roll once all the numbers are used.
    ///        ... --Play(move)--> GameOver when a player has won (all 4 tokens home; Blitz: one).
    /// </summary>
    public sealed class LudoGame
    {
        readonly RulesConfig config;
        readonly IDiceSource dice;
        readonly List<Move> legal = new List<Move>(Board.TokensPerPlayer * 3);
        readonly List<Move> scratch = new List<Move>(Board.TokensPerPlayer);
        readonly List<TokenRef> capturedBuffer = new List<TokenRef>(Board.TokensPerPlayer);
        readonly List<int> pending = new List<int>(3);        // Ludo Star rule: numbers rolled this turn, not played yet
        bool bonusRoll;                                        // Ludo Star rule: a capture / home earned a roll after the numbers

        public GameState State { get; }
        public TurnPhase Phase => State.Phase;
        public int CurrentPlayer => State.CurrentPlayer;
        public int LastRoll => State.LastRoll;
        public IReadOnlyList<Move> LegalMoves => legal;
        public IReadOnlyList<int> PendingRolls => pending;
        public GameMode Mode => config.Mode;
        public bool RollAgainOnSix => config.RollAgainOnSix;

        public event Action<RollResult> Rolled;
        public event Action<MoveResult> Moved;
        public event Action<int> TurnChanged;   // argument = the player whose turn it now is
        public event Action<int> GameWon;       // argument = winning player

        public LudoGame(int[] seatPerPlayer, RulesConfig config, IDiceSource dice)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.dice = dice ?? throw new ArgumentNullException(nameof(dice));
            State = new GameState(seatPerPlayer) { Teams = config.IsTeams };
            if (config.Mode == GameMode.Blitz)
            {
                State.StartProgressAtReset = Board.StartProgress;     // Blitz: every pawn starts on its start cell
                State.Reset();
            }
        }

        public void Restart()
        {
            State.Reset();
            legal.Clear();
            pending.Clear();
            bonusRoll = false;
            TurnChanged?.Invoke(State.CurrentPlayer);
        }

        public RollResult Roll()
        {
            if (Phase != TurnPhase.WaitingForRoll)
                throw new InvalidOperationException("Cannot roll now (phase = " + Phase + ").");

            int player = State.CurrentPlayer;
            int value = dice.Next();
            if (value < 1 || value > 6) throw new InvalidOperationException("Dice source returned " + value + ".");

            State.LastRoll = value;
            State.ConsecutiveSixes = value == 6 ? State.ConsecutiveSixes + 1 : 0;

            if (value == 6 && config.MaxConsecutiveSixes > 0 && State.ConsecutiveSixes >= config.MaxConsecutiveSixes)
                return Pass(player, value, PassReason.ThreeSixes);

            if (config.RollAgainOnSix)
            {
                pending.Add(value);
                if (value == 6 && CanHoldAnother)                        // roll again before anything moves
                {
                    legal.Clear();
                    var again = new RollResult(player, value, Array.Empty<Move>(), PassReason.None, rollAgain: true, pending: pending.ToArray());
                    Rolled?.Invoke(again);
                    return again;
                }
                CollectMoves(player);
            }
            else Rules.GetLegalMoves(State, config, player, value, legal);

            if (legal.Count == 0)
                return Pass(player, value, PassReason.NoLegalMoves);

            State.Phase = TurnPhase.WaitingForMove;
            var result = new RollResult(player, value, legal.ToArray(), PassReason.None, pending: config.RollAgainOnSix ? pending.ToArray() : null);
            Rolled?.Invoke(result);
            return result;
        }

        /// <summary>May another number be added to the waiting list? (RulesConfig.PendingDiceLimit; 0 = no limit.)</summary>
        bool CanHoldAnother => config.PendingDiceLimit <= 0 || pending.Count < config.PendingDiceLimit;

        /// <summary>
        /// Ludo Star rule: the moves of the numbers still to be played (each Move carries its number in Move.Roll).
        /// FreeChoice offers every waiting number at once; Fifo/Lifo offer only the first playable one from their end, so
        /// a number that no pawn can use never blocks the turn.
        /// </summary>
        void CollectMoves(int player)
        {
            legal.Clear();
            if (config.DiceSelection == DiceSelectionPolicy.FreeChoice)
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    if (pending.IndexOf(pending[i]) != i) continue;      // the same number twice gives the same moves
                    Rules.GetLegalMoves(State, config, player, pending[i], scratch);
                    legal.AddRange(scratch);
                }
                return;
            }
            bool oldestFirst = config.DiceSelection == DiceSelectionPolicy.Fifo;
            for (int n = 0; n < pending.Count; n++)
            {
                int i = oldestFirst ? n : pending.Count - 1 - n;
                Rules.GetLegalMoves(State, config, player, pending[i], scratch);
                if (scratch.Count == 0) continue;
                legal.AddRange(scratch);
                return;
            }
        }

        public MoveResult Play(Move move)
        {
            if (Phase != TurnPhase.WaitingForMove)
                throw new InvalidOperationException("Cannot play a move now (phase = " + Phase + ").");
            if (!legal.Contains(move))
                throw new ArgumentException("That move is not legal.");

            capturedBuffer.Clear();
            bool reachedHome = Rules.ApplyMove(State, config, move, capturedBuffer);
            bool won = Rules.HasWon(State, config, move.Player);
            legal.Clear();

            if (config.RollAgainOnSix && !won)
            {
                pending.Remove(move.Roll);
                bonusRoll |= (capturedBuffer.Count > 0 && config.ExtraTurnOnCapture) || (reachedHome && config.ExtraTurnOnReachHome);
                if (pending.Count > 0)
                {
                    CollectMoves(move.Player);
                    if (legal.Count > 0)                                // more numbers to play: same player, still moving
                    {
                        var more = new MoveResult(move, capturedBuffer.ToArray(), reachedHome, false, false, moreMoves: true);
                        Moved?.Invoke(more);
                        return more;
                    }
                    pending.Clear();                                    // the numbers left cannot be used by any pawn
                }
                bool again = bonusRoll;
                bonusRoll = false;
                var done = new MoveResult(move, capturedBuffer.ToArray(), reachedHome, false, again);
                if (again)
                {
                    State.ConsecutiveSixes = 0;
                    State.Phase = TurnPhase.WaitingForRoll;
                    Moved?.Invoke(done);
                }
                else
                {
                    Moved?.Invoke(done);
                    NextTurn();
                }
                return done;
            }

            bool extra = !won && (
                (move.Roll == 6 && config.ExtraTurnOnSix) ||
                (capturedBuffer.Count > 0 && config.ExtraTurnOnCapture) ||
                (reachedHome && config.ExtraTurnOnReachHome));

            var result = new MoveResult(move, capturedBuffer.ToArray(), reachedHome, won, extra);

            if (won)
            {
                pending.Clear();
                State.Winner = move.Player;
                State.Phase = TurnPhase.GameOver;
                Moved?.Invoke(result);
                GameWon?.Invoke(move.Player);
            }
            else if (extra)
            {
                State.Phase = TurnPhase.WaitingForRoll;
                Moved?.Invoke(result);
            }
            else
            {
                Moved?.Invoke(result);
                NextTurn();
            }
            return result;
        }

        RollResult Pass(int player, int value, PassReason reason)
        {
            legal.Clear();
            pending.Clear();
            bonusRoll = false;
            var result = new RollResult(player, value, Array.Empty<Move>(), reason);
            Rolled?.Invoke(result);
            NextTurn();
            return result;
        }

        void NextTurn()
        {
            State.ConsecutiveSixes = 0;
            pending.Clear();
            bonusRoll = false;
            int next = State.CurrentPlayer;
            for (int i = 0; i < State.PlayerCount; i++)
            {
                next = (next + 1) % State.PlayerCount;
                // TeamUp: a partner who is already home has nothing to roll for - hand the dice straight on
                if (!State.Teams || !Rules.IsDone(State, next)) break;
            }
            State.CurrentPlayer = next;
            State.Phase = TurnPhase.WaitingForRoll;
            TurnChanged?.Invoke(State.CurrentPlayer);
        }
    }
}
