using System;
using System.Collections.Generic;
using Ludo.Core;

namespace Ludo.AI
{
    /// <summary>
    /// Medium's priorities (capture, finish, leave base, safe cells ...) PLUS awareness of what happens next:
    ///  - DANGER: an unsafe token that an enemy can reach with one roll is likely to be captured, so moves that
    ///    reduce danger are rewarded and moves that add danger are punished.
    ///  - THREAT: moves that put my tokens within one roll of an unsafe enemy token are rewarded.
    /// Every move is tried on a copy of the game and the danger/threat before and after are compared.
    /// The weights (2.0 and 1.0) were chosen by playing thousands of seeded games against Medium
    /// (~60% wins for Hard); see AiTests.
    /// </summary>
    public sealed class HardAi : IAiPlayer
    {
        const double DangerWeight = 2.0;
        const double ThreatWeight = 1.0;

        readonly Random rng;

        public HardAi(int seed) { rng = new Random(seed); }

        public Move Choose(GameState state, RulesConfig config, IReadOnlyList<Move> legalMoves)
        {
            int player = legalMoves[0].Player;
            double dangerBefore = Danger(state, player);
            double threatBefore = Threat(state, player);

            Move best = legalMoves[0];
            double bestScore = double.NegativeInfinity;
            var captured = new List<TokenRef>(4);
            for (int i = 0; i < legalMoves.Count; i++)
            {
                captured.Clear();
                GameState after = MoveSim.After(state, config, legalMoves[i], captured, out bool _);

                double score = MediumAi.PriorityScore(state, config, legalMoves[i])
                             + DangerWeight * (dangerBefore - Danger(after, player))
                             + ThreatWeight * (Threat(after, player) - threatBefore)
                             + rng.NextDouble() * 0.5;                     // tiny noise breaks ties
                if (score <= bestScore) continue;
                bestScore = score;
                best = legalMoves[i];
            }
            return best;
        }

        /// <summary>How much value is at risk: every unsafe token on the shared loop, weighted by how likely an enemy hits it.</summary>
        public static double Danger(GameState s, int player)
        {
            double total = 0;
            for (int t = 0; t < Board.TokensPerPlayer; t++)
            {
                int progress = s.GetProgress(player, t);
                int cell = Board.ToOuterCell(s.SeatOf(player), progress);
                if (cell < 0 || Board.IsSafeCell(cell)) continue;           // in base, home column, home or on a safe cell
                int attackers = 0;
                for (int q = 0; q < s.PlayerCount; q++)
                {
                    if (s.SameTeam(q, player)) continue;                   // TeamUp: a partner is not an attacker
                    for (int u = 0; u < Board.TokensPerPlayer; u++)
                        if (CanReach(s, q, u, cell)) attackers++;
                }
                total += Value(progress) * Math.Min(1.0, attackers / 6.0);  // each attacker needs one number out of six
            }
            return total;
        }

        /// <summary>The same idea seen from the other side: enemy tokens that I could capture with one roll.</summary>
        public static double Threat(GameState s, int player)
        {
            double total = 0;
            for (int q = 0; q < s.PlayerCount; q++)
            {
                if (s.SameTeam(q, player)) continue;                       // TeamUp: never aim at a partner
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                {
                    int progress = s.GetProgress(q, t);
                    int cell = Board.ToOuterCell(s.SeatOf(q), progress);
                    if (cell < 0 || Board.IsSafeCell(cell)) continue;
                    int attackers = 0;
                    for (int u = 0; u < Board.TokensPerPlayer; u++)
                        if (CanReach(s, player, u, cell)) attackers++;
                    total += Value(progress) * Math.Min(1.0, attackers / 6.0);
                }
            }
            return total;
        }

        static double Value(int progress) => 10 + progress;   // the further a token has travelled, the more losing it hurts

        /// <summary>Can this token land exactly on the outer cell with a roll of 1..6, staying on the shared loop?</summary>
        static bool CanReach(GameState s, int player, int token, int cell)
        {
            int progress = s.GetProgress(player, token);
            int from = Board.ToOuterCell(s.SeatOf(player), progress);
            if (from < 0) return false;
            int distance = (cell - from + Board.OuterCells) % Board.OuterCells;
            int stepsLeftOnLoop = progress == Board.LapProgress ? 6 : Board.LastOuterProgress - progress;   // after that the token turns into its home column
            return distance >= 1 && distance <= 6 && distance <= stepsLeftOnLoop;
        }
    }
}
