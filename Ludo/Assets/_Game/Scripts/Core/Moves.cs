using System;

namespace Ludo.Core
{
    /// <summary>One token going from one progress value to another.</summary>
    public readonly struct Move : IEquatable<Move>
    {
        public readonly int Player;
        public readonly int Token;
        public readonly int From;
        public readonly int To;
        public readonly int Roll;

        public Move(int player, int token, int from, int to, int roll)
        {
            Player = player; Token = token; From = from; To = to; Roll = roll;
        }

        public bool Equals(Move o) => Player == o.Player && Token == o.Token && From == o.From && To == o.To && Roll == o.Roll;
        public override bool Equals(object obj) => obj is Move m && Equals(m);
        public override int GetHashCode() => (((Player * 4 + Token) * 64 + (From + 1)) * 64 + To) * 8 + Roll;
    }

    public readonly struct TokenRef
    {
        public readonly int Player;
        public readonly int Token;
        public TokenRef(int player, int token) { Player = player; Token = token; }
    }

    public enum PassReason { None, NoLegalMoves, ThreeSixes }

    /// <summary>What happened when the dice was rolled. If Pass != None the turn was already handed over.</summary>
    public sealed class RollResult
    {
        public readonly int Player;
        public readonly int Value;
        public readonly Move[] LegalMoves;
        public readonly PassReason Pass;
        /// <summary>Ludo Star rule: a 6 was rolled - roll again before moving (LegalMoves is empty).</summary>
        public readonly bool RollAgain;
        /// <summary>Every number rolled this turn that still has to be played (Ludo Star rule; just Value otherwise).</summary>
        public readonly int[] Pending;

        public RollResult(int player, int value, Move[] legalMoves, PassReason pass, bool rollAgain = false, int[] pending = null)
        {
            Player = player; Value = value; LegalMoves = legalMoves; Pass = pass; RollAgain = rollAgain;
            Pending = pending ?? (pass == PassReason.None && !rollAgain ? new[] { value } : new int[0]);
        }
    }

    /// <summary>What happened when a move was played.</summary>
    public sealed class MoveResult
    {
        public readonly Move Move;
        public readonly TokenRef[] Captured;
        public readonly bool ReachedHome;
        public readonly bool GameWon;
        public readonly bool ExtraTurn;
        /// <summary>Ludo Star rule: more rolled numbers are waiting - the same player moves again now (see LudoGame.LegalMoves).</summary>
        public readonly bool MoreMoves;

        public MoveResult(Move move, TokenRef[] captured, bool reachedHome, bool gameWon, bool extraTurn, bool moreMoves = false)
        {
            Move = move; Captured = captured; ReachedHome = reachedHome; GameWon = gameWon; ExtraTurn = extraTurn; MoreMoves = moreMoves;
        }
    }
}
