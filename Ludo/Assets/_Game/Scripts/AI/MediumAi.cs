using System;
using System.Collections.Generic;
using Ludo.Core;

namespace Ludo.AI
{
    /// <summary>
    /// Follows simple priorities: capture > reach home > leave base > enter the home column > land on a safe cell >
    /// advance. It does NOT look at what the opponents can do next (that is what Hard adds).
    /// </summary>
    public sealed class MediumAi : IAiPlayer
    {
        readonly Random rng;

        public MediumAi(int seed) { rng = new Random(seed); }

        public Move Choose(GameState state, RulesConfig config, IReadOnlyList<Move> legalMoves)
        {
            Move best = legalMoves[0];
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < legalMoves.Count; i++)
            {
                double score = PriorityScore(state, config, legalMoves[i]) + rng.NextDouble() * 0.5;   // tiny noise breaks ties
                if (score <= bestScore) continue;
                bestScore = score;
                best = legalMoves[i];
            }
            return best;
        }

        /// <summary>The priority score of a move. Also used by HardAi as its starting point.</summary>
        internal static double PriorityScore(GameState state, RulesConfig config, Move move)
        {
            var captured = new List<TokenRef>(4);
            MoveSim.After(state, config, move, captured, out bool reachedHome);   // only the capture list + reachedHome are needed
            double score = move.To * 0.3;                                   // prefer advancing far tokens a little

            foreach (var c in captured)                                     // capturing hurts the opponent more the further they got
                score += 60 + state.GetProgress(c.Player, c.Token) * 0.5;
            if (reachedHome) score += 70;
            if (move.From == Board.BaseProgress) score += 40;               // get tokens into play
            if (move.From <= Board.LastOuterProgress && Board.IsInHomeColumn(move.To)) score += 20;

            int cell = Board.ToOuterCell(state.SeatOf(move.Player), move.To);
            if (cell >= 0 && Board.IsSafeCell(cell)) score += 12;
            return score;
        }
    }
}
