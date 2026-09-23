using System;

namespace Ludo.Core
{
    public enum TurnPhase { WaitingForRoll, WaitingForMove, GameOver }

    /// <summary>
    /// All the data of a game in progress, and nothing else (no rules, no Unity).
    /// Small and cloneable, so the AI can try moves on a copy without touching the real game.
    /// Only the Core assembly can change it; everyone else reads.
    /// </summary>
    public sealed class GameState
    {
        readonly int[] seats;       // seat (0..3) of each player, in turn order
        readonly int[] progress;    // [player * 4 + token] -> progress (see Board)
        readonly bool[] captured;   // Master mode: has this player captured an opponent pawn yet (then its pawns may enter home)

        public int PlayerCount => seats.Length;
        public int CurrentPlayer { get; internal set; }
        public int ConsecutiveSixes { get; internal set; }
        public int LastRoll { get; internal set; }
        public TurnPhase Phase { get; internal set; }
        public int Winner { get; internal set; } = -1;

        public GameState(int[] seatPerPlayer)
        {
            if (seatPerPlayer == null || seatPerPlayer.Length < 2 || seatPerPlayer.Length > Board.MaxPlayers)
                throw new ArgumentException("Ludo needs 2-4 players.");
            for (int i = 0; i < seatPerPlayer.Length; i++)
            {
                if (seatPerPlayer[i] < 0 || seatPerPlayer[i] >= Board.MaxPlayers)
                    throw new ArgumentException("Seat must be 0-3.");
                for (int j = 0; j < i; j++)
                    if (seatPerPlayer[i] == seatPerPlayer[j]) throw new ArgumentException("Two players share a seat.");
            }
            seats = (int[])seatPerPlayer.Clone();
            progress = new int[seats.Length * Board.TokensPerPlayer];
            captured = new bool[seats.Length];
            Reset();
        }

        GameState(GameState other)
        {
            seats = other.seats; // seats never change, safe to share
            progress = (int[])other.progress.Clone();
            captured = (bool[])other.captured.Clone();
            StartProgressAtReset = other.StartProgressAtReset;
            CurrentPlayer = other.CurrentPlayer;
            ConsecutiveSixes = other.ConsecutiveSixes;
            LastRoll = other.LastRoll;
            Phase = other.Phase;
            Winner = other.Winner;
        }

        public int SeatOf(int player) => seats[player];

        public int GetProgress(int player, int token) => progress[player * Board.TokensPerPlayer + token];

        internal void SetProgress(int player, int token, int value) => progress[player * Board.TokensPerPlayer + token] = value;

        /// <summary>Has this player captured at least one opponent pawn in this game? (Master mode lets its pawns home only then)</summary>
        public bool HasCaptured(int player) => captured[player];

        internal void SetCaptured(int player) => captured[player] = true;

        /// <summary>Where every pawn stands when the game (re)starts: in base, or on its start cell (Blitz).</summary>
        internal int StartProgressAtReset { get; set; } = Board.BaseProgress;

        public GameState Clone() => new GameState(this);

        internal void Reset()
        {
            for (int i = 0; i < progress.Length; i++) progress[i] = StartProgressAtReset;
            for (int i = 0; i < captured.Length; i++) captured[i] = false;
            CurrentPlayer = 0;
            ConsecutiveSixes = 0;
            LastRoll = 0;
            Phase = TurnPhase.WaitingForRoll;
            Winner = -1;
        }
    }
}
