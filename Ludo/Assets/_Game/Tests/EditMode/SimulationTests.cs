using System;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    /// <summary>Plays whole games with random (but legal) choices to prove the engine never gets stuck
    /// or reaches an impossible position.</summary>
    public class SimulationTests
    {
        const int MaxRolls = 100000;

        static (int rolls, int winner, int[] finalProgress) PlayOut(int players, int seed)
        {
            var game = new LudoGame(Board.DefaultSeats(players), new RulesConfig(), new RandomDiceSource(seed));
            var chooser = new Random(seed * 7919 + 1);
            int rolls = 0;

            while (game.Phase != TurnPhase.GameOver)
            {
                Assert.Less(rolls, MaxRolls, "game did not finish (stuck?) players=" + players + " seed=" + seed);
                var roll = game.Roll();
                rolls++;
                if (roll.Pass == PassReason.None)
                {
                    var pick = roll.LegalMoves[chooser.Next(roll.LegalMoves.Length)];
                    game.Play(pick);
                }
                AssertInvariants(game.State);
            }

            var progress = new int[players * Board.TokensPerPlayer];
            for (int p = 0; p < players; p++)
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                    progress[p * Board.TokensPerPlayer + t] = game.State.GetProgress(p, t);
            return (rolls, game.State.Winner, progress);
        }

        static void AssertInvariants(GameState s)
        {
            for (int p = 0; p < s.PlayerCount; p++)
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                {
                    int pr = s.GetProgress(p, t);
                    Assert.GreaterOrEqual(pr, Board.BaseProgress);
                    Assert.LessOrEqual(pr, Board.FinishProgress);
                }
        }

        [Test]
        public void RandomGames_AlwaysFinish_WithAValidWinner([Values(2, 3, 4)] int players)
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var r = PlayOut(players, seed);
                Assert.GreaterOrEqual(r.winner, 0);
                Assert.Less(r.winner, players);
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                    Assert.AreEqual(Board.FinishProgress, r.finalProgress[r.winner * Board.TokensPerPlayer + t]);
            }
        }

        [Test]
        public void SameSeed_GivesIdenticalGame()
        {
            var a = PlayOut(4, 12345);
            var b = PlayOut(4, 12345);
            Assert.AreEqual(a.rolls, b.rolls);
            Assert.AreEqual(a.winner, b.winner);
            CollectionAssert.AreEqual(a.finalProgress, b.finalProgress);
        }
    }
}
