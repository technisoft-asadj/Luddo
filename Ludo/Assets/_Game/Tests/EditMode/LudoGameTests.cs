using System;
using System.Linq;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    public class LudoGameTests
    {
        // ---------- setup / guards ----------

        [Test]
        public void NewGame_StartsWithPlayerZeroWaitingForRoll()
        {
            var g = T.Game(2, new ScriptedDice());
            Assert.AreEqual(TurnPhase.WaitingForRoll, g.Phase);
            Assert.AreEqual(0, g.CurrentPlayer);
        }

        [Test]
        public void Constructor_RejectsBadSetups()
        {
            var cfg = new RulesConfig();
            Assert.Throws<ArgumentException>(() => new LudoGame(new[] { 0 }, cfg, new ScriptedDice()));
            Assert.Throws<ArgumentException>(() => new LudoGame(new[] { 0, 1, 2, 3, 0 }, cfg, new ScriptedDice()));
            Assert.Throws<ArgumentException>(() => new LudoGame(new[] { 1, 1 }, cfg, new ScriptedDice()));
            Assert.Throws<ArgumentException>(() => new LudoGame(new[] { 0, 4 }, cfg, new ScriptedDice()));
        }

        [Test]
        public void Roll_WhileWaitingForMove_Throws()
        {
            var g = T.Game(2, new ScriptedDice(6, 6));
            g.Roll();
            Assert.AreEqual(TurnPhase.WaitingForMove, g.Phase);
            Assert.Throws<InvalidOperationException>(() => g.Roll());
        }

        [Test]
        public void Play_BeforeRolling_Throws()
        {
            var g = T.Game(2, new ScriptedDice());
            Assert.Throws<InvalidOperationException>(() => g.Play(new Move(0, 0, -1, 0, 6)));
        }

        [Test]
        public void Play_IllegalMove_Throws()
        {
            var g = T.Game(2, new ScriptedDice(6));
            g.Roll();
            Assert.Throws<ArgumentException>(() => g.Play(new Move(0, 0, -1, 5, 6)));
            Assert.AreEqual(TurnPhase.WaitingForMove, g.Phase, "a rejected move must not change the game");
        }

        [Test]
        public void BadDiceValue_Throws()
        {
            var g = T.Game(2, new ScriptedDice(7));
            Assert.Throws<InvalidOperationException>(() => g.Roll());
        }

        // ---------- passing ----------

        [Test]
        public void NoLegalMoves_PassesTheTurn()
        {
            var g = T.Game(2, new ScriptedDice(3));
            int changed = -1;
            g.TurnChanged += p => changed = p;

            var r = g.Roll();

            Assert.AreEqual(PassReason.NoLegalMoves, r.Pass);
            Assert.AreEqual(0, r.LegalMoves.Length);
            Assert.AreEqual(1, g.CurrentPlayer);
            Assert.AreEqual(TurnPhase.WaitingForRoll, g.Phase);
            Assert.AreEqual(1, changed);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void TurnOrder_CyclesThroughAllPlayers(int players)
        {
            var g = T.Game(players, new ScriptedDice(Enumerable.Repeat(1, players * 2).ToArray()));
            for (int i = 0; i < players * 2; i++)
            {
                Assert.AreEqual(i % players, g.CurrentPlayer);
                g.Roll(); // everyone is in base, so a 1 always passes
            }
            Assert.AreEqual(0, g.CurrentPlayer);
        }

        // ---------- extra turns ----------

        [Test]
        public void Six_LeavesBase_AndGivesExtraTurn()
        {
            var g = T.Game(2, new ScriptedDice(6));
            var roll = g.Roll();
            Assert.AreEqual(4, roll.LegalMoves.Length);

            var result = g.Play(roll.LegalMoves[0]);

            Assert.IsTrue(result.ExtraTurn);
            Assert.AreEqual(0, g.CurrentPlayer);
            Assert.AreEqual(TurnPhase.WaitingForRoll, g.Phase);
            Assert.AreEqual(Board.StartProgress, g.State.GetProgress(0, 0));
        }

        [Test]
        public void NormalMove_EndsTheTurn()
        {
            var g = T.Game(2, new ScriptedDice(3));
            T.Put(g, 0, 0, 10);
            var result = g.Play(g.Roll().LegalMoves[0]);
            Assert.IsFalse(result.ExtraTurn);
            Assert.AreEqual(1, g.CurrentPlayer);
        }

        [Test]
        public void Capture_GivesExtraTurn()
        {
            var g = T.Game(2, new ScriptedDice(1));
            T.Put(g, 0, 0, 5);
            T.Put(g, 1, 0, T.ProgressForCell(2, 6));

            var result = g.Play(g.Roll().LegalMoves[0]);

            Assert.AreEqual(1, result.Captured.Length);
            Assert.IsTrue(result.ExtraTurn);
            Assert.AreEqual(0, g.CurrentPlayer);
        }

        [Test]
        public void ReachingHome_GivesExtraTurn()
        {
            var g = T.Game(2, new ScriptedDice(3));
            T.Put(g, 0, 0, 53);
            var result = g.Play(g.Roll().LegalMoves[0]);
            Assert.IsTrue(result.ReachedHome);
            Assert.IsTrue(result.ExtraTurn);
            Assert.AreEqual(0, g.CurrentPlayer);
        }

        [Test]
        public void ExtraTurnOnSix_CanBeDisabled()
        {
            var cfg = new RulesConfig { ExtraTurnOnSix = false };
            var g = T.Game(2, new ScriptedDice(6), cfg);
            var result = g.Play(g.Roll().LegalMoves[0]);
            Assert.IsFalse(result.ExtraTurn);
            Assert.AreEqual(1, g.CurrentPlayer);
        }

        // ---------- three sixes ----------

        [Test]
        public void ThreeSixesInARow_ForfeitsTheTurn()
        {
            var g = T.Game(2, new ScriptedDice(6, 6, 6));
            T.Put(g, 0, 0, 10);

            g.Play(g.Roll().LegalMoves[0]);                 // 10 -> 16, extra turn
            g.Play(g.Roll().LegalMoves[0]);                 // 16 -> 22, extra turn
            var third = g.Roll();                           // third six

            Assert.AreEqual(PassReason.ThreeSixes, third.Pass);
            Assert.AreEqual(22, g.State.GetProgress(0, 0), "the third six is not played");
            Assert.AreEqual(1, g.CurrentPlayer);
            Assert.AreEqual(0, g.State.ConsecutiveSixes);
        }

        [Test]
        public void NonSix_ResetsTheSixCounter()
        {
            var g = T.Game(2, new ScriptedDice(6, 3, 6, 6));
            T.Put(g, 0, 0, 10);
            T.Put(g, 1, 0, 10);

            g.Play(g.Roll().LegalMoves[0]);                 // six, extra turn
            g.Play(g.Roll().LegalMoves[0]);                 // three, turn passes
            Assert.AreEqual(1, g.CurrentPlayer);
            Assert.AreEqual(0, g.State.ConsecutiveSixes);

            g.Play(g.Roll().LegalMoves[0]);                 // player 1: six
            g.Play(g.Roll().LegalMoves[0]);                 // player 1: second six, still allowed
            Assert.AreEqual(1, g.CurrentPlayer);
        }

        // ---------- winning ----------

        [Test]
        public void LastTokenHome_WinsTheGame()
        {
            var g = T.Game(2, new ScriptedDice(3));
            for (int t = 0; t < 3; t++) T.Put(g, 0, t, Board.FinishProgress);
            T.Put(g, 0, 3, 53);
            int winner = -1;
            g.GameWon += p => winner = p;

            var result = g.Play(g.Roll().LegalMoves[0]);

            Assert.IsTrue(result.GameWon);
            Assert.IsFalse(result.ExtraTurn);
            Assert.AreEqual(TurnPhase.GameOver, g.Phase);
            Assert.AreEqual(0, g.State.Winner);
            Assert.AreEqual(0, winner);
            Assert.Throws<InvalidOperationException>(() => g.Roll());
        }

        [Test]
        public void Restart_ResetsEverything()
        {
            var g = T.Game(2, new ScriptedDice(6));
            g.Play(g.Roll().LegalMoves[0]);
            g.Restart();

            Assert.AreEqual(TurnPhase.WaitingForRoll, g.Phase);
            Assert.AreEqual(0, g.CurrentPlayer);
            Assert.AreEqual(0, g.State.ConsecutiveSixes);
            for (int t = 0; t < 4; t++) Assert.AreEqual(Board.BaseProgress, g.State.GetProgress(0, t));
        }

        // ---------- events ----------

        [Test]
        public void Events_AreRaisedInOrder()
        {
            var g = T.Game(2, new ScriptedDice(3));
            T.Put(g, 0, 0, 10);
            var log = new System.Collections.Generic.List<string>();
            g.Rolled += r => log.Add("rolled");
            g.Moved += m => log.Add("moved");
            g.TurnChanged += p => log.Add("turn" + p);

            g.Play(g.Roll().LegalMoves[0]);

            CollectionAssert.AreEqual(new[] { "rolled", "moved", "turn1" }, log);
        }
    }

    // Small convenience so tests read naturally: T.Put(game, ...) already exists for LudoGame.
}
