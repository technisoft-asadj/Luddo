using System;
using System.Collections.Generic;
using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    public class BoardGridTests
    {
        static float Dist(GridPoint a, GridPoint b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        static void AssertAt(GridPoint p, float x, float y, string what)
        {
            Assert.AreEqual(x, p.X, 0.001f, what + " x");
            Assert.AreEqual(y, p.Y, 0.001f, what + " y");
        }

        [Test]
        public void OuterLoop_Has52DistinctCells_InsideTheBoard()
        {
            var seen = new HashSet<(float, float)>();
            for (int c = 0; c < Board.OuterCells; c++)
            {
                var p = BoardGrid.OuterCell(c);
                Assert.Greater(p.X, 0f); Assert.Less(p.X, BoardGrid.Size);
                Assert.Greater(p.Y, 0f); Assert.Less(p.Y, BoardGrid.Size);
                Assert.IsTrue(seen.Add((p.X, p.Y)), "duplicate position for cell " + c);
            }
        }

        [Test]
        public void OuterLoop_IsContinuous_AndClosed()
        {
            // neighbouring cells touch (distance 1) or cut a corner diagonally (distance sqrt 2)
            for (int c = 0; c < Board.OuterCells; c++)
            {
                float d = Dist(BoardGrid.OuterCell(c), BoardGrid.OuterCell((c + 1) % Board.OuterCells));
                Assert.LessOrEqual(d, 1.415f, "gap after cell " + c);
                Assert.GreaterOrEqual(d, 0.999f, "overlap after cell " + c);
            }
        }

        [Test]
        public void StartCells_MatchTheBoardImage()
        {
            AssertAt(BoardGrid.ForProgress((int)Seat.Red, 0, 0), 1.5f, 6.5f, "red start");
            AssertAt(BoardGrid.ForProgress((int)Seat.Green, 0, 0), 8.5f, 1.5f, "green start");
            AssertAt(BoardGrid.ForProgress((int)Seat.Yellow, 0, 0), 13.5f, 8.5f, "yellow start");
            AssertAt(BoardGrid.ForProgress((int)Seat.Blue, 0, 0), 6.5f, 13.5f, "blue start");
        }

        [Test]
        public void StarCells_MatchTheBoardImage()
        {
            AssertAt(BoardGrid.OuterCell(8), 6.5f, 2.5f, "red star");
            AssertAt(BoardGrid.OuterCell(21), 12.5f, 6.5f, "green star");
            AssertAt(BoardGrid.OuterCell(34), 8.5f, 12.5f, "yellow star");
            AssertAt(BoardGrid.OuterCell(47), 2.5f, 8.5f, "blue star");
        }

        [Test]
        public void EverySeat_PathIsContinuous_FromStartToFinish()
        {
            for (int seat = 0; seat < Board.MaxPlayers; seat++)
                for (int p = Board.StartProgress; p < Board.FinishProgress; p++)
                {
                    float d = Dist(BoardGrid.ForProgress(seat, 0, p), BoardGrid.ForProgress(seat, 0, p + 1));
                    Assert.LessOrEqual(d, 1.5f, "seat " + seat + " jump after progress " + p);
                    Assert.Greater(d, 0.3f, "seat " + seat + " no movement after progress " + p);
                }
        }

        [Test]
        public void HomeColumns_RunFromTheEdgeToTheCentre()
        {
            AssertAt(BoardGrid.HomeColumn((int)Seat.Red, 0), 1.5f, 7.5f, "red first");
            AssertAt(BoardGrid.HomeColumn((int)Seat.Red, 4), 5.5f, 7.5f, "red last");
            AssertAt(BoardGrid.HomeColumn((int)Seat.Green, 0), 7.5f, 1.5f, "green first");
            AssertAt(BoardGrid.HomeColumn((int)Seat.Green, 4), 7.5f, 5.5f, "green last");
            AssertAt(BoardGrid.HomeColumn((int)Seat.Yellow, 0), 13.5f, 7.5f, "yellow first");
            AssertAt(BoardGrid.HomeColumn((int)Seat.Yellow, 4), 9.5f, 7.5f, "yellow last");
            AssertAt(BoardGrid.HomeColumn((int)Seat.Blue, 0), 7.5f, 13.5f, "blue first");
            AssertAt(BoardGrid.HomeColumn((int)Seat.Blue, 4), 7.5f, 9.5f, "blue last");
        }

        [Test]
        public void BaseSlots_AreFourDistinctSpots_InEachCorner()
        {
            for (int seat = 0; seat < Board.MaxPlayers; seat++)
            {
                var spots = new HashSet<(float, float)>();
                for (int t = 0; t < 4; t++)
                {
                    var p = BoardGrid.BaseSlot(seat, t);
                    spots.Add((p.X, p.Y));
                    Assert.IsTrue(p.X < 6f || p.X > 9f, "base slot not in a corner (x)");
                    Assert.IsTrue(p.Y < 6f || p.Y > 9f, "base slot not in a corner (y)");
                }
                Assert.AreEqual(4, spots.Count, "seat " + seat);
            }
        }

        [Test]
        public void FinishSpots_AreDistinct_AndNextToTheCentre()
        {
            for (int seat = 0; seat < Board.MaxPlayers; seat++)
            {
                var spots = new HashSet<(float, float)>();
                for (int t = 0; t < 4; t++)
                {
                    var p = BoardGrid.FinishSpot(seat, t);
                    spots.Add((p.X, p.Y));
                    Assert.LessOrEqual(Dist(p, new GridPoint(7.5f, 7.5f)), 1.2f);
                }
                Assert.AreEqual(4, spots.Count, "seat " + seat);
            }
        }

        [Test]
        public void ForProgress_RejectsOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardGrid.ForProgress(0, 0, 58));       // (57 = the Master-mode lap cell, a real position)
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardGrid.ForProgress(0, 0, -2));
        }
    }
}
