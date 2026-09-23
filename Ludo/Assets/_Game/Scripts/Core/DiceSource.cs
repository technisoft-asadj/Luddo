using System;
using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>Where dice values come from. The engine never rolls dice itself, so tests can script rolls
    /// and an online game can use the value the host decided.</summary>
    public interface IDiceSource
    {
        /// <summary>Returns 1..6.</summary>
        int Next();
    }

    public sealed class RandomDiceSource : IDiceSource
    {
        readonly Random rng;

        public RandomDiceSource() : this(Environment.TickCount) { }
        public RandomDiceSource(int seed) { rng = new Random(seed); }

        public int Next() => rng.Next(1, 7);
    }

    /// <summary>Hands out values that somebody else decided (online: the host). Push a value, then let the engine roll.</summary>
    public sealed class QueuedDiceSource : IDiceSource
    {
        readonly Queue<int> values = new Queue<int>();

        public int Count => values.Count;

        public void Push(int value)
        {
            if (value < 1 || value > 6) throw new ArgumentOutOfRangeException(nameof(value), "A die shows 1 to 6.");
            values.Enqueue(value);
        }

        public int Next()
        {
            if (values.Count == 0) throw new InvalidOperationException("No dice value is waiting.");
            return values.Dequeue();
        }
    }
}
