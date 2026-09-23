using System;
using System.Collections.Generic;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    public class BoardTests
    {
        [Test]
        public void StartCells_AreThirteenApart()
        {
            Assert.AreEqual(0, Board.StartCell((int)Seat.Red));
            Assert.AreEqual(13, Board.StartCell((int)Seat.Green));
            Assert.AreEqual(26, Board.StartCell((int)Seat.Yellow));
            Assert.AreEqual(39, Board.StartCell((int)Seat.Blue));
        }

        [Test]
        public void ToOuterCell_MapsProgressToSharedLoop()
        {
            Assert.AreEqual(13, Board.ToOuterCell((int)Seat.Green, 0));
            Assert.AreEqual(37, Board.ToOuterCell((int)Seat.Blue, 50));   // (39+50) % 52
            Assert.AreEqual(2, Board.ToOuterCell((int)Seat.Yellow, 28));  // wraps around the loop
        }

        [TestCase(-1)]   // base
        [TestCase(51)]   // home column
        [TestCase(55)]
        [TestCase(56)]   // finished
        public void ToOuterCell_IsMinusOne_OffTheSharedLoop(int progress)
        {
            Assert.AreEqual(-1, Board.ToOuterCell((int)Seat.Red, progress));
        }

        [Test]
        public void SafeCells_AreStartAndStarCellsOnly()
        {
            var expected = new HashSet<int> { 0, 8, 13, 21, 26, 34, 39, 47 };
            for (int cell = 0; cell < Board.OuterCells; cell++)
                Assert.AreEqual(expected.Contains(cell), Board.IsSafeCell(cell), "cell " + cell);
        }

        [Test]
        public void EverySeat_WalksFiftyOneDistinctOuterCells()
        {
            for (int seat = 0; seat < Board.MaxPlayers; seat++)
            {
                var cells = new HashSet<int>();
                for (int p = 0; p <= Board.LastOuterProgress; p++) cells.Add(Board.ToOuterCell(seat, p));
                Assert.AreEqual(51, cells.Count, "seat " + seat);
            }
        }

        [Test]
        public void ProgressClassification_IsExclusive()
        {
            Assert.IsTrue(Board.IsInBase(-1));
            Assert.IsTrue(Board.IsOnOuterTrack(0));
            Assert.IsTrue(Board.IsOnOuterTrack(50));
            Assert.IsTrue(Board.IsInHomeColumn(51));
            Assert.IsTrue(Board.IsInHomeColumn(55));
            Assert.IsTrue(Board.IsFinished(56));
            Assert.IsFalse(Board.IsInHomeColumn(56));
            Assert.IsFalse(Board.IsOnOuterTrack(51));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void DefaultSeats_AreDistinct(int players)
        {
            var seats = Board.DefaultSeats(players);
            Assert.AreEqual(players, seats.Length);
            Assert.AreEqual(players, new HashSet<int>(seats).Count);
        }

        [TestCase(1)]
        [TestCase(5)]
        public void DefaultSeats_RejectsBadPlayerCount(int players)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Board.DefaultSeats(players));
        }
    }
}
