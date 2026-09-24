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
    ///   TeamUp  - 2 vs 2 on the classic rules: the two seats facing each other are partners (Red+Yellow against
    ///             Green+Blue). Partners never capture each other, a partner who has finished is skipped, and a team
    ///             wins only when BOTH of its players have all four pawns home. Always four players.
    /// </summary>
    public enum GameMode { Classic = 0, Master = 1, Arrow = 2, Blitz = 3, TeamUp = 4 }

    /// <summary>
    /// Which of the numbers a player has rolled and not yet played may be used next.
    ///   FreeChoice - any of them, in the order the player likes (the default, Ludo Star style).
    ///   Fifo       - the oldest number first.
    ///   Lifo       - the newest number first.
    /// </summary>
    public enum DiceSelectionPolicy { FreeChoice = 0, Fifo = 1, Lifo = 2 }

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

        /// <summary>Which of the numbers still waiting may be played next (Ludo Star style = the player's free choice).</summary>
        public DiceSelectionPolicy DiceSelection = DiceSelectionPolicy.FreeChoice;

        /// <summary>How many rolled numbers may wait to be played at once. 0 = no limit (the three-sixes rule caps it anyway).</summary>
        public int PendingDiceLimit = 0;

        /// <summary>When exactly one pawn can use a number, move it without asking.</summary>
        public bool AutoSelectSingleLegalToken = true;

        /// <summary>
        /// How many times a person may take back a move in one match. 0 = no Undo at all, which is what Classic and every
        /// ranked game use.
        ///
        /// Undo puts the pawn back and returns the number to the pile still to be played - it does NOT rewind past the
        /// roll. That is deliberate: re-rolling would let a player undo their way to a six, which would make the dice
        /// unfair, and this game promises a fair dice everywhere else. Undo fixes a mis-tap; it never buys better luck.
        /// </summary>
        public int UndosPerMatch = 0;

        public bool UndoAllowed => UndosPerMatch > 0;

        /// <summary>2 vs 2: the seats facing each other are partners (see GameMode.TeamUp).</summary>
        public bool IsTeams => Mode == GameMode.TeamUp;

        /// <summary>A copy with another mode (the shared rules asset is never changed).</summary>
        public RulesConfig WithMode(GameMode mode)
        {
            var copy = (RulesConfig)MemberwiseClone();
            copy.Mode = mode;
            return copy;
        }
    }
}
