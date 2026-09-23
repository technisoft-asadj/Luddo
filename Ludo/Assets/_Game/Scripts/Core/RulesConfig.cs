using System;

namespace Ludo.Core
{
    /// <summary>
    /// The game modes (like Ludo Star's tables). All run on the same engine; only these rule differences apply:
    ///   Classic - the standard rules.
    ///   Master  - a pawn may enter its home path only after its player has captured at least one opponent pawn; until then it
    ///             passes its home entrance and goes round the board again.
    ///   Arrow   - four arrow cells (2 cells after each start cell): a pawn that stops on one slides along the arrow 6 cells
    ///             forward to the next star cell.
    ///   Blitz   - every pawn starts on its start cell (not in base) and the first player to bring ONE pawn home wins.
    /// </summary>
    public enum GameMode { Classic = 0, Master = 1, Arrow = 2, Blitz = 3 }

    /// <summary>House-rule switches. Defaults = the rules agreed in PLAN.md (Decision 2).</summary>
    [Serializable]
    public sealed class RulesConfig
    {
        public bool SixToLeaveBase = true;
        public bool ExtraTurnOnSix = true;
        public bool ExtraTurnOnCapture = true;
        public bool ExtraTurnOnReachHome = true;

        /// <summary>The Nth six in a row forfeits the turn. 0 disables the rule.</summary>
        public int MaxConsecutiveSixes = 3;

        public bool ExactRollToFinish = true;

        /// <summary>false = safe cells protect tokens (default).</summary>
        public bool CaptureOnSafeCells = false;

        /// <summary>
        /// Ludo Star style: a 6 means "roll again" before anything moves; when a non-6 comes (or the Nth six forfeits the turn)
        /// the player plays every number rolled, one move per number, in the order they choose (6 then 5, or 5 then 6).
        /// A capture or reaching home earns one more roll after all the numbers are used. Off = the classic one-roll-one-move turn.
        /// </summary>
        public bool RollAgainOnSix = false;

        /// <summary>Which game mode the rules follow (see GameMode).</summary>
        public GameMode Mode = GameMode.Classic;

        /// <summary>A copy with another mode (the shared rules asset is never changed).</summary>
        public RulesConfig WithMode(GameMode mode)
        {
            var copy = (RulesConfig)MemberwiseClone();
            copy.Mode = mode;
            return copy;
        }
    }
}
