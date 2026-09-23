using System;
using System.Collections.Generic;
using Ludo.Core;

namespace Ludo.AI
{
    /// <summary>Picks any legal move at random. Beginner-friendly: it never plans, so it misses captures too.</summary>
    public sealed class EasyAi : IAiPlayer
    {
        readonly Random rng;

        public EasyAi(int seed) { rng = new Random(seed); }

        public Move Choose(GameState state, RulesConfig config, IReadOnlyList<Move> legalMoves) =>
            legalMoves[rng.Next(legalMoves.Count)];
    }
}
