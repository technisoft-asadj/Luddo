using System.Collections.Generic;
using Ludo.Core;

namespace Ludo.AI
{
    public enum AiDifficulty { Easy, Medium, Hard }

    /// <summary>
    /// A computer player. It is handed the game state and the moves the ENGINE says are legal, and must return
    /// one of them. Because it can only pick from that list, the AI can never break a rule.
    /// It must not change the state it is given (clone it to try moves out).
    /// </summary>
    public interface IAiPlayer
    {
        Move Choose(GameState state, RulesConfig config, IReadOnlyList<Move> legalMoves);
    }

    public static class AiFactory
    {
        /// <param name="seed">Only used to break ties between equally good moves (keeps games reproducible).</param>
        public static IAiPlayer Create(AiDifficulty difficulty, int seed)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy: return new EasyAi(seed);
                case AiDifficulty.Medium: return new MediumAi(seed);
                default: return new HardAi(seed);
            }
        }
    }

    /// <summary>Shared helper: play a move on a COPY of the state so the AI can look at the result.</summary>
    internal static class MoveSim
    {
        internal static GameState After(GameState state, RulesConfig config, Move move, List<TokenRef> captured, out bool reachedHome)
        {
            GameState copy = state.Clone();
            reachedHome = Rules.ApplyMove(copy, config, move, captured);
            return copy;
        }
    }
}
