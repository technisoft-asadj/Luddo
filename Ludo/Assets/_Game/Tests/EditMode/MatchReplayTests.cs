using System;
using System.Collections.Generic;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    public class MatchReplayTests
    {
        /// <summary>Plays a whole game the way the online flow does and records the story the host would keep.</summary>
        static List<int> PlayAndRecord(int players, int seed, int stopAfterEvents, out LudoGame game, out RollResult waiting)
        {
            var rng = new Random(seed);
            var dice = new QueuedDiceSource();
            game = new LudoGame(Board.DefaultSeats(players), new RulesConfig(), dice);
            var story = new List<int>();
            waiting = null;
            int guard = 0;
            while (game.Phase != TurnPhase.GameOver && guard++ < 5000)
            {
                if (story.Count >= stopAfterEvents) return story;
                int value = rng.Next(1, 7);
                story.Add(value);
                dice.Push(value);
                var roll = game.Roll();
                if (roll.Pass != PassReason.None) continue;
                if (MatchReplay.OnlyOneChoice(roll))
                {
                    game.Play(roll.LegalMoves[0]);
                    continue;
                }
                if (story.Count >= stopAfterEvents) { waiting = roll; return story; }   // stopped between the roll and the pick
                int pick = rng.Next(roll.LegalMoves.Length);
                story.Add(MatchReplay.PickBase + pick);
                game.Play(roll.LegalMoves[pick]);
            }
            return story;
        }

        static void AssertSame(GameState a, GameState b, string context)
        {
            Assert.AreEqual(a.CurrentPlayer, b.CurrentPlayer, context + " current player");
            Assert.AreEqual(a.Phase, b.Phase, context + " phase");
            Assert.AreEqual(a.Winner, b.Winner, context + " winner");
            Assert.AreEqual(a.ConsecutiveSixes, b.ConsecutiveSixes, context + " sixes");
            for (int p = 0; p < a.PlayerCount; p++)
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                    Assert.AreEqual(a.GetProgress(p, t), b.GetProgress(p, t), context + " token " + p + "-" + t);
        }

        [Test]
        public void ReplayingTheWholeStoryGivesTheSameFinishedGame()
        {
            for (int players = 2; players <= 4; players++)
                for (int seed = 1; seed <= 6; seed++)
                {
                    var story = PlayAndRecord(players, seed, int.MaxValue, out var original, out _);
                    var dice = new QueuedDiceSource();
                    var copy = new LudoGame(Board.DefaultSeats(players), new RulesConfig(), dice);
                    var waiting = MatchReplay.Apply(copy, dice, story);
                    Assert.IsNull(waiting);
                    AssertSame(original.State, copy.State, players + " players, seed " + seed);
                    Assert.AreEqual(TurnPhase.GameOver, copy.Phase);
                }
        }

        [Test]
        public void ReplayingAnyPartOfTheStoryGivesTheSamePosition()
        {
            // a phone can come back at any moment: after every possible number of events
            for (int stop = 0; stop < 120; stop += 3)
            {
                var story = PlayAndRecord(3, 42, stop, out var original, out var waitingOriginal);
                var dice = new QueuedDiceSource();
                var copy = new LudoGame(Board.DefaultSeats(3), new RulesConfig(), dice);
                var waiting = MatchReplay.Apply(copy, dice, story);
                AssertSame(original.State, copy.State, "stopped after " + stop + " events");
                Assert.AreEqual(waitingOriginal != null, waiting != null, "a roll waiting for its pick, after " + stop);
                if (waiting != null) Assert.AreEqual(waitingOriginal.Value, waiting.Value);
            }
        }

        [Test]
        public void AnEmptyStoryIsANewGame()
        {
            var dice = new QueuedDiceSource();
            var game = new LudoGame(Board.DefaultSeats(2), new RulesConfig(), dice);
            Assert.IsNull(MatchReplay.Apply(game, dice, new int[0]));
            Assert.AreEqual(0, game.State.CurrentPlayer);
            Assert.AreEqual(TurnPhase.WaitingForRoll, game.Phase);
        }

        [Test]
        public void ABadPickIndexIsClampedNotFatal()
        {
            var dice = new QueuedDiceSource();
            var game = new LudoGame(Board.DefaultSeats(2), new RulesConfig(), dice);
            // 6 lets a token out; then a wild pick number must not throw
            Assert.DoesNotThrow(() => MatchReplay.Apply(game, dice, new[] { 6, 99, 3, 17 }));
        }
    }
}
