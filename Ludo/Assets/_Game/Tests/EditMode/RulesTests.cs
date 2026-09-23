using System.Collections.Generic;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    // Two players: player 0 = Red seat (start cell 0), player 1 = Yellow seat (start cell 26).
    public class RulesTests
    {
        GameState state;
        RulesConfig config;
        List<Move> moves;
        List<TokenRef> captured;

        [SetUp]
        public void SetUp()
        {
            state = new GameState(Board.DefaultSeats(2));
            config = new RulesConfig();
            moves = new List<Move>();
            captured = new List<TokenRef>();
        }

        // ---------- legal moves ----------

        [Test]
        public void AllInBase_NeedsASix()
        {
            Rules.GetLegalMoves(state, config, 0, 5, moves);
            Assert.AreEqual(0, moves.Count);

            Rules.GetLegalMoves(state, config, 0, 6, moves);
            Assert.AreEqual(4, moves.Count);
            foreach (var m in moves) Assert.AreEqual(Board.StartProgress, m.To);
        }

        [Test]
        public void SixNotRequired_WhenConfigured()
        {
            config.SixToLeaveBase = false;
            Rules.GetLegalMoves(state, config, 0, 3, moves);
            Assert.AreEqual(4, moves.Count);
        }

        [Test]
        public void TokenOnTrack_MovesByRoll()
        {
            T.Put(state, 0, 0, 10);
            Rules.GetLegalMoves(state, config, 0, 4, moves);
            Assert.AreEqual(1, moves.Count);
            Assert.AreEqual(new Move(0, 0, 10, 14, 4), moves[0]);
        }

        [Test]
        public void ExactRoll_IsRequiredToFinish()
        {
            T.Put(state, 0, 0, 54);
            Rules.GetLegalMoves(state, config, 0, 3, moves);
            Assert.AreEqual(0, moves.Count, "overshoot is illegal");

            Rules.GetLegalMoves(state, config, 0, 2, moves);
            Assert.AreEqual(1, moves.Count);
            Assert.AreEqual(Board.FinishProgress, moves[0].To);
        }

        [Test]
        public void Overshoot_Finishes_WhenExactRollNotRequired()
        {
            config.ExactRollToFinish = false;
            T.Put(state, 0, 0, 54);
            Rules.GetLegalMoves(state, config, 0, 5, moves);
            Assert.AreEqual(Board.FinishProgress, moves[0].To);
        }

        [Test]
        public void FinishedToken_CannotMove()
        {
            T.Put(state, 0, 0, Board.FinishProgress);
            Rules.GetLegalMoves(state, config, 0, 6, moves);
            Assert.AreEqual(3, moves.Count, "only the 3 tokens still in base");
            foreach (var m in moves) Assert.AreNotEqual(0, m.Token);
        }

        [Test]
        public void GetLegalMoves_ClearsTheList()
        {
            moves.Add(new Move(0, 0, 0, 0, 0));
            Rules.GetLegalMoves(state, config, 0, 1, moves);
            Assert.AreEqual(0, moves.Count);
        }

        // ---------- capture ----------

        [Test]
        public void Landing_OnOpponent_Captures()
        {
            T.Put(state, 0, 0, 5);                                   // Red on cell 5
            T.Put(state, 1, 0, T.ProgressForCell(2, 6));             // Yellow on cell 6
            Rules.ApplyMove(state, config, new Move(0, 0, 5, 6, 1), captured);

            Assert.AreEqual(1, captured.Count);
            Assert.AreEqual(1, captured[0].Player);
            Assert.AreEqual(0, captured[0].Token);
            Assert.AreEqual(Board.BaseProgress, state.GetProgress(1, 0));
            Assert.AreEqual(6, state.GetProgress(0, 0));
        }

        [Test]
        public void Landing_OnSafeCell_DoesNotCapture()
        {
            T.Put(state, 0, 0, 5);
            int yellowOn8 = T.ProgressForCell(2, 8);                 // cell 8 = a star cell
            T.Put(state, 1, 0, yellowOn8);
            Rules.ApplyMove(state, config, new Move(0, 0, 5, 8, 3), captured);

            Assert.AreEqual(0, captured.Count);
            Assert.AreEqual(yellowOn8, state.GetProgress(1, 0));
        }

        [Test]
        public void Landing_OnSafeCell_Captures_WhenConfigured()
        {
            config.CaptureOnSafeCells = true;
            T.Put(state, 0, 0, 5);
            T.Put(state, 1, 0, T.ProgressForCell(2, 8));
            Rules.ApplyMove(state, config, new Move(0, 0, 5, 8, 3), captured);
            Assert.AreEqual(1, captured.Count);
        }

        [Test]
        public void Landing_CapturesEveryOpponentTokenOnTheCell()
        {
            T.Put(state, 0, 0, 5);
            int p = T.ProgressForCell(2, 6);
            T.Put(state, 1, 0, p);
            T.Put(state, 1, 1, p);
            Rules.ApplyMove(state, config, new Move(0, 0, 5, 6, 1), captured);
            Assert.AreEqual(2, captured.Count);
        }

        [Test]
        public void OwnTokens_CanStack_AndAreNeverCaptured()
        {
            T.Put(state, 0, 0, 5);
            T.Put(state, 0, 1, 6);
            Rules.ApplyMove(state, config, new Move(0, 0, 5, 6, 1), captured);
            Assert.AreEqual(0, captured.Count);
            Assert.AreEqual(6, state.GetProgress(0, 1));
        }

        [Test]
        public void HomeColumn_IsPrivate_NoCapture()
        {
            T.Put(state, 0, 0, 50);
            T.Put(state, 1, 0, 52);                                  // Yellow in ITS home column
            Rules.ApplyMove(state, config, new Move(0, 0, 50, 52, 2), captured);
            Assert.AreEqual(0, captured.Count);
            Assert.AreEqual(52, state.GetProgress(1, 0));
        }

        [Test]
        public void LeavingBase_OntoOccupiedStartCell_Coexists()
        {
            T.Put(state, 1, 0, T.ProgressForCell(2, 0));             // Yellow sitting on Red's start cell (safe)
            Rules.ApplyMove(state, config, new Move(0, 0, -1, 0, 6), captured);
            Assert.AreEqual(0, captured.Count);
            Assert.AreEqual(Board.StartProgress, state.GetProgress(0, 0));
        }

        [Test]
        public void ApplyMove_ReportsReachingHome()
        {
            T.Put(state, 0, 0, 53);
            Assert.IsTrue(Rules.ApplyMove(state, config, new Move(0, 0, 53, 56, 3), captured));
            T.Put(state, 0, 1, 10);
            Assert.IsFalse(Rules.ApplyMove(state, config, new Move(0, 1, 10, 13, 3), captured));
        }

        // ---------- winning / cloning ----------

        [Test]
        public void HasWon_OnlyWhenAllFourAreHome()
        {
            for (int t = 0; t < 3; t++) T.Put(state, 0, t, Board.FinishProgress);
            Assert.IsFalse(Rules.HasWon(state, 0));
            T.Put(state, 0, 3, Board.FinishProgress);
            Assert.IsTrue(Rules.HasWon(state, 0));
            Assert.IsFalse(Rules.HasWon(state, 1));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            T.Put(state, 0, 0, 20);
            var copy = state.Clone();
            T.Put(copy, 0, 0, 30);
            Assert.AreEqual(20, state.GetProgress(0, 0));
            Assert.AreEqual(30, copy.GetProgress(0, 0));
        }
    }
}
