using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    /// <summary>The local person sits bottom-right in every match, and Team Up partners can be chosen by sitting across from them.</summary>
    public class SeatingTests
    {
        [TestCase(2, 0), TestCase(2, 1), TestCase(3, 0), TestCase(3, 1), TestCase(3, 2), TestCase(4, 0), TestCase(4, 1), TestCase(4, 2), TestCase(4, 3)]
        public void LocalPlayerSitsInTheTargetSeat_AndOrderIsKept(int players, int me)
        {
            var original = Board.DefaultSeats(players);
            var seats = Board.SeatsWithPlayerAt(players, me, (int)Seat.Yellow);
            Assert.AreEqual((int)Seat.Yellow, seats[me]);
            for (int i = 0; i < players; i++)
                for (int j = 0; j < players; j++)
                    Assert.AreEqual((original[j] - original[i] + 4) % 4, (seats[j] - seats[i] + 4) % 4, "relative positions must not change");
            CollectionAssert.AllItemsAreUnique(seats);
        }

        [TestCase(GameMode.Classic), TestCase(GameMode.Master), TestCase(GameMode.Arrow), TestCase(GameMode.Blitz), TestCase(GameMode.TeamUp)]
        public void TurnedTableStillPlaysTheSameGame(GameMode mode)
        {
            // the same dice on a turned table: every pawn ends on the same progress, so phones that turn the table differently agree
            var cfg = new RulesConfig().WithMode(mode);
            var a = new LudoGame(Board.DefaultSeats(4), cfg, new RandomDiceSource(11));
            var b = new LudoGame(Board.SeatsWithPlayerAt(4, 1, (int)Seat.Yellow), cfg, new RandomDiceSource(11));
            for (int step = 0; step < 4000 && a.Phase != TurnPhase.GameOver && b.Phase != TurnPhase.GameOver; step++)
            {
                var ra = a.Roll();
                var rb = b.Roll();
                Assert.AreEqual(ra.Pass, rb.Pass);
                Assert.AreEqual(ra.LegalMoves.Length, rb.LegalMoves.Length);
                if (ra.Pass != PassReason.None || ra.LegalMoves.Length == 0) continue;
                a.Play(ra.LegalMoves[0]);
                b.Play(rb.LegalMoves[0]);
            }
            for (int p = 0; p < 4; p++)
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                    Assert.AreEqual(a.State.GetProgress(p, t), b.State.GetProgress(p, t));
            Assert.AreEqual(a.State.Winner, b.State.Winner);
        }

        static readonly string[] Ids = { "a", "b", "c", "d" };

        [Test]
        public void NoChoice_KeepsJoinOrder() =>
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, TeamSeating.Arrange(Ids, new[] { "", "", "", "" }));

        [Test]
        public void ChosenPartnerSitsAcross()
        {
            var order = TeamSeating.Arrange(Ids, new[] { "d", "", "", "" });        // a picks d
            Assert.AreEqual(0, order[0]);
            Assert.AreEqual(3, order[2]);                                           // d across from a
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, order);
        }

        [Test]
        public void ChosenByTheOtherSide_StillPairs()
        {
            var order = TeamSeating.Arrange(Ids, new[] { "", "", "", "a" });        // d picks a
            Assert.AreEqual(0, order[0]);
            Assert.AreEqual(3, order[2]);
        }

        [Test]
        public void TwoSeparateChoices_BothHonoured()
        {
            var order = TeamSeating.Arrange(Ids, new[] { "c", "d", "", "" });       // a+c, b+d
            Assert.AreEqual(2, order[0] + order[2]);                                // 0 and 2 face each other
            Assert.AreEqual(4, order[1] + order[3]);                                // 1 and 3 face each other
        }

        [Test]
        public void ClashingChoices_NeverSeatAnyoneTwice()
        {
            var order = TeamSeating.Arrange(Ids, new[] { "b", "c", "b", "b" });
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, order);
        }

        [Test]
        public void NotFourPeople_NothingChanges() =>
            CollectionAssert.AreEqual(new[] { 0, 1 }, TeamSeating.Arrange(new[] { "a", "b" }, new[] { "b", "a" }));
    }
}
