using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>
    /// The story of an online match as a list of numbers: 1..6 = the dice showed that, 10+n = move number n was picked.
    /// A phone that was away plays the story again instantly on a fresh engine and ends in exactly the same position as
    /// everybody else (the engine is deterministic). Pure and unit-tested; GameController only uses it.
    /// </summary>
    public static class MatchReplay
    {
        public const int PickBase = 10;

        /// <summary>
        /// When every legal move is the same move (several tokens in the base, or the same number rolled twice), nobody is asked
        /// to choose: the first one is played. Different numbers are a real choice (6 first or 5 first).
        /// </summary>
        public static bool OnlyOneChoice(RollResult roll) => OnlyOneChoice(roll.LegalMoves);

        public static bool OnlyOneChoice(IReadOnlyList<Move> moves)
        {
            for (int i = 1; i < moves.Count; i++)
                if (moves[i].From != moves[0].From || moves[i].Roll != moves[0].Roll) return false;
            return moves.Count > 0;
        }

        /// <summary>
        /// Play the story on 'game' (which must be new and use 'dice'). Returns the moves still waiting for a pick (as a
        /// RollResult), or null when the story ends where nobody has to choose (between turns, or waiting for a roll).
        /// </summary>
        public static RollResult Apply(LudoGame game, QueuedDiceSource dice, IEnumerable<int> story)
        {
            RollResult waiting = null;
            foreach (int e in story)
            {
                if (e < PickBase)
                {
                    dice.Push(e);
                    var roll = game.Roll();
                    waiting = null;
                    if (roll.Pass != PassReason.None || roll.RollAgain) continue;   // the engine handed the turn on, or rolls again
                    waiting = AutoPlay(game);
                }
                else if (waiting != null)
                {
                    int index = System.Math.Max(0, System.Math.Min(e - PickBase, waiting.LegalMoves.Length - 1));
                    game.Play(waiting.LegalMoves[index]);
                    waiting = game.Phase == TurnPhase.WaitingForMove ? AutoPlay(game) : null;
                }
            }
            return waiting;
        }

        /// <summary>Play every move that needs no pick (no pick is announced for those); return the choice that is left, if any.</summary>
        static RollResult AutoPlay(LudoGame game)
        {
            while (game.Phase == TurnPhase.WaitingForMove)
            {
                var moves = new Move[game.LegalMoves.Count];
                for (int i = 0; i < moves.Length; i++) moves[i] = game.LegalMoves[i];
                if (!OnlyOneChoice(moves)) return new RollResult(game.CurrentPlayer, game.LastRoll, moves, PassReason.None);
                game.Play(moves[0]);
            }
            return null;
        }
    }
}
