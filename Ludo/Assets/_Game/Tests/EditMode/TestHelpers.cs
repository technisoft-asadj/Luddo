using System;
using System.Collections.Generic;
using Ludo.Core;

namespace Ludo.Tests
{
    /// <summary>Dice that returns exactly the values we give it, so a test can play a chosen scenario.</summary>
    public sealed class ScriptedDice : IDiceSource
    {
        readonly Queue<int> values;
        public ScriptedDice(params int[] scripted) { values = new Queue<int>(scripted); }
        public int Next()
        {
            if (values.Count == 0) throw new InvalidOperationException("ScriptedDice ran out of values.");
            return values.Dequeue();
        }
    }

    public static class T
    {
        public static LudoGame Game(int players, ScriptedDice dice, RulesConfig config = null) =>
            new LudoGame(Board.DefaultSeats(players), config ?? new RulesConfig(), dice);

        /// <summary>Put a token at a chosen progress (test setup only).</summary>
        public static void Put(LudoGame g, int player, int token, int progress) => g.State.SetProgress(player, token, progress);

        public static void Put(GameState s, int player, int token, int progress) => s.SetProgress(player, token, progress);

        /// <summary>Progress that puts a token of the given seat on the given outer cell.</summary>
        public static int ProgressForCell(int seat, int cell) => (cell - Board.StartCell(seat) + Board.OuterCells) % Board.OuterCells;
    }
}
