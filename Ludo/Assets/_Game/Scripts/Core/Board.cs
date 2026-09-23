namespace Ludo.Core
{
    /// <summary>Corner of the board, numbered clockwise. The number is the seat's position on the loop.</summary>
    public enum Seat { Red = 0, Green = 1, Yellow = 2, Blue = 3 }

    /// <summary>
    /// Pure geometry of the board. No state lives here.
    ///
    /// A token's position is ONE int called "progress" (relative to its own seat):
    ///   -1      in base (not on the board yet)
    ///    0      its own start cell
    ///    0..50  the shared outer loop
    ///   51..55  its private home column
    ///   56      finished (home)
    ///   57      Master mode only: the last outer cell before its own start cell, reached only when a pawn goes round
    ///           again because its player has not captured anyone yet (normally a pawn turns into its home path before it)
    /// The 52 outer cells are numbered 0..51 clockwise; seats start at cells 0, 13, 26, 39.
    /// </summary>
    public static class Board
    {
        public const int OuterCells = 52;
        public const int TokensPerPlayer = 4;
        public const int MaxPlayers = 4;

        public const int BaseProgress = -1;
        public const int StartProgress = 0;
        public const int LastOuterProgress = 50;
        public const int HomeColumnStart = 51;
        public const int FinishProgress = 56;
        public const int LapProgress = 57;

        /// <summary>Arrow mode: arrows start 2 cells after each start cell and end 6 cells further, on the star cell.</summary>
        public const int ArrowOffset = 2, ArrowJump = 6;

        const int CellsBetweenSeats = 13;
        const int StarOffset = 8; // the star (safe) cell is 8 cells after each start cell

        public static int StartCell(int seat) => seat * CellsBetweenSeats;

        public static bool IsInBase(int progress) => progress == BaseProgress;
        public static bool IsOnOuterTrack(int progress) => (progress >= StartProgress && progress <= LastOuterProgress) || progress == LapProgress;

        /// <summary>How many cells past its own start cell a pawn on the shared loop is (0..51).</summary>
        public static int LoopOffset(int progress) => progress == LapProgress ? OuterCells - 1 : progress;
        public static bool IsInHomeColumn(int progress) => progress >= HomeColumnStart && progress < FinishProgress;
        public static bool IsFinished(int progress) => progress == FinishProgress;

        /// <summary>Shared outer-loop cell (0..51) for a token, or -1 if it is not on the shared loop.</summary>
        public static int ToOuterCell(int seat, int progress) =>
            IsOnOuterTrack(progress) ? (StartCell(seat) + LoopOffset(progress)) % OuterCells : -1;

        /// <summary>Arrow mode: does an arrow start on this shared-loop cell?</summary>
        public static bool IsArrowCell(int cell) => cell >= 0 && cell % CellsBetweenSeats == ArrowOffset;

        /// <summary>Safe cells: the four start cells and the four star cells.</summary>
        public static bool IsSafeCell(int cell)
        {
            int r = cell % CellsBetweenSeats;
            return r == 0 || r == StarOffset;
        }

        /// <summary>Seats used for a given number of players (2 = opposite corners).</summary>
        public static int[] DefaultSeats(int playerCount)
        {
            switch (playerCount)
            {
                case 2: return new[] { (int)Seat.Red, (int)Seat.Yellow };
                case 3: return new[] { (int)Seat.Red, (int)Seat.Green, (int)Seat.Yellow };
                case 4: return new[] { (int)Seat.Red, (int)Seat.Green, (int)Seat.Yellow, (int)Seat.Blue };
                default: throw new System.ArgumentOutOfRangeException(nameof(playerCount), "Ludo supports 2-4 players.");
            }
        }
    }
}
