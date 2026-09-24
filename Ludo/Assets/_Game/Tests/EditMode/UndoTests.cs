using System.Linq;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    /// <summary>
    /// Undo: saving a position and putting it back. The rule that matters most is that Undo restores the WHOLE position -
    /// pawns, the numbers still to play, the consecutive-six counter and the earned bonus roll - because a half-undo is
    /// how a player would cheat.
    /// </summary>
    public class UndoTests
    {
        static RulesConfig Star()
        {
            var c = new RulesConfig();
            c.RollAgainOnSix = true;
            c.UndosPerMatch = 3;
            return c;
        }

        [Test]
        public void UndoIsOffUnlessTheModeAsksForIt()
        {
            Assert.AreEqual(0, new RulesConfig().UndosPerMatch, "Classic must be untouched by default");
            Assert.IsFalse(new RulesConfig().UndoAllowed);
            Assert.IsTrue(Star().UndoAllowed);
        }

        [Test]
        public void TakingBackAMovePutsThePawnAndTheNumberBack()
        {
            var g = T.Game(2, new ScriptedDice(4), new RulesConfig());
            T.Put(g, 0, 0, 10);
            g.Roll();
            var before = g.Save();
            var move = g.LegalMoves.First(m => m.Token == 0);
            g.Play(move);
            Assert.AreEqual(14, g.State.GetProgress(0, 0));

            g.Restore(before);
            Assert.AreEqual(10, g.State.GetProgress(0, 0), "the pawn went back");
            Assert.AreEqual(TurnPhase.WaitingForMove, g.Phase, "and it is still the same player's move");
            Assert.AreEqual(0, g.CurrentPlayer);
            Assert.AreEqual(4, g.LastRoll, "the number is unchanged: Undo never re-rolls");
            Assert.IsTrue(g.LegalMoves.Any(m => m.Token == 0 && m.To == 14), "the move is on offer again");
        }

        [Test]
        public void ACapturedPawnComesBackOutOfBase()
        {
            var g = T.Game(2, new ScriptedDice(3), new RulesConfig());
            int cell = 20;
            int victim = T.ProgressForCell(g.State.SeatOf(1), cell);
            T.Put(g, 1, 0, victim);
            T.Put(g, 0, 0, T.ProgressForCell(g.State.SeatOf(0), cell) - 3);
            g.Roll();
            var before = g.Save();
            g.Play(g.LegalMoves.First(m => m.Token == 0));
            Assert.AreEqual(Board.BaseProgress, g.State.GetProgress(1, 0), "the capture happened");

            g.Restore(before);
            Assert.AreEqual(victim, g.State.GetProgress(1, 0), "the captured pawn is back where it stood");
        }

        [Test]
        public void TheNumbersStillToPlayComeBackToo()
        {
            var g = T.Game(2, new ScriptedDice(6, 5), Star());
            T.Put(g, 0, 0, 10);
            g.Roll();
            g.Roll();
            CollectionAssert.AreEquivalent(new[] { 6, 5 }, g.PendingRolls.ToArray());
            var before = g.Save();

            g.Play(g.LegalMoves.First(m => m.Token == 0 && m.Roll == 5));
            CollectionAssert.AreEquivalent(new[] { 6 }, g.PendingRolls.ToArray(), "the 5 was used up");

            g.Restore(before);
            CollectionAssert.AreEquivalent(new[] { 6, 5 }, g.PendingRolls.ToArray(), "both numbers are waiting again");
            Assert.AreEqual(10, g.State.GetProgress(0, 0));
            Assert.IsTrue(g.LegalMoves.Any(m => m.Roll == 5) && g.LegalMoves.Any(m => m.Roll == 6));
        }

        [Test]
        public void TheConsecutiveSixCounterIsRestored()
        {
            var g = T.Game(2, new ScriptedDice(6, 6, 2), Star());
            T.Put(g, 0, 0, 10);
            g.Roll();
            g.Roll();
            Assert.AreEqual(2, g.State.ConsecutiveSixes);
            var before = g.Save();
            g.Roll();
            Assert.AreEqual(0, g.State.ConsecutiveSixes, "a non-six resets it");
            g.Restore(before);
            Assert.AreEqual(2, g.State.ConsecutiveSixes, "a half-undo would hand the player a free third six");
        }

        [Test]
        public void MasterModeRemembersWhetherYouHadCapturedYet()
        {
            var config = new RulesConfig().WithMode(GameMode.Master);
            var g = T.Game(2, new ScriptedDice(3), config);
            int cell = 20;
            T.Put(g, 1, 0, T.ProgressForCell(g.State.SeatOf(1), cell));
            T.Put(g, 0, 0, T.ProgressForCell(g.State.SeatOf(0), cell) - 3);
            g.Roll();
            var before = g.Save();
            g.Play(g.LegalMoves.First(m => m.Token == 0));
            Assert.IsTrue(g.State.HasCaptured(0), "capturing opened the home path");

            g.Restore(before);
            Assert.IsFalse(g.State.HasCaptured(0), "Undo shuts it again - otherwise Undo would hand out the mode's reward");
        }

        [Test]
        public void ASavedPositionIsNotAffectedByLaterPlay()
        {
            var g = T.Game(2, new ScriptedDice(4, 4), new RulesConfig());
            T.Put(g, 0, 0, 10);
            g.Roll();
            var before = g.Save();
            g.Play(g.LegalMoves.First(m => m.Token == 0));
            g.Restore(before);
            g.Play(g.LegalMoves.First(m => m.Token == 0));      // play it again from the restored position
            Assert.AreEqual(14, g.State.GetProgress(0, 0), "restoring twice from one snapshot gives the same result");
            g.Restore(before);
            Assert.AreEqual(10, g.State.GetProgress(0, 0));
        }

        [Test]
        public void RestoringBeforeAMoveOffersExactlyTheSameMoves()
        {
            var g = T.Game(4, new ScriptedDice(6), new RulesConfig());
            g.Roll();
            var expected = g.LegalMoves.ToArray();
            var before = g.Save();
            g.Play(expected[0]);
            g.Restore(before);
            CollectionAssert.AreEquivalent(expected, g.LegalMoves.ToArray());
        }
    }
}
