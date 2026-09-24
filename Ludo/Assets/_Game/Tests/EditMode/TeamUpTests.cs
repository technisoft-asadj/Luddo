using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ludo.AI;
using Ludo.Core;

namespace Ludo.Tests
{
    /// <summary>
    /// Team Up (2 vs 2): partners never capture each other, a finished partner is skipped, and a side wins only when BOTH
    /// of its players are home. Players 0..3 sit Red, Green, Yellow, Blue, so 0+2 are partners and 1+3 are partners.
    /// </summary>
    public class TeamUpTests
    {
        static RulesConfig Cfg() => new RulesConfig().WithMode(GameMode.TeamUp);

        static LudoGame Game(ScriptedDice dice = null) => T.Game(4, dice ?? new ScriptedDice(), Cfg());

        static void Finish(LudoGame g, int player)
        {
            for (int t = 0; t < Board.TokensPerPlayer; t++) T.Put(g, player, t, Board.FinishProgress);
        }

        // ---------- who is on which side ----------

        [Test]
        public void SeatsFacingEachOtherArePartners()
        {
            var g = Game();
            Assert.IsTrue(g.State.Teams);
            Assert.IsTrue(g.State.SameTeam(0, 2), "Red and Yellow");
            Assert.IsTrue(g.State.SameTeam(1, 3), "Green and Blue");
            Assert.IsFalse(g.State.SameTeam(0, 1));
            Assert.IsFalse(g.State.SameTeam(2, 3));
            Assert.AreEqual(g.State.TeamOf(0), g.State.TeamOf(2));
            Assert.AreNotEqual(g.State.TeamOf(0), g.State.TeamOf(1));
        }

        [Test]
        public void EveryOtherModeLeavesEveryPlayerOnTheirOwnSide()
        {
            foreach (var mode in new[] { GameMode.Classic, GameMode.Master, GameMode.Arrow, GameMode.Blitz })
            {
                var g = T.Game(4, new ScriptedDice(), new RulesConfig().WithMode(mode));
                Assert.IsFalse(g.State.Teams, mode.ToString());
                Assert.IsFalse(g.State.SameTeam(0, 2), mode.ToString());
            }
        }

        // ---------- no friendly capture ----------

        [Test]
        public void LandingOnYourPartnerDoesNotSendItHome()
        {
            var g = Game();
            var config = Cfg();
            int cell = 20;                                                  // an ordinary, unprotected cell
            T.Put(g, 2, 0, T.ProgressForCell(g.State.SeatOf(2), cell));      // the partner is standing there
            int from = T.ProgressForCell(g.State.SeatOf(0), cell) - 3;
            T.Put(g, 0, 0, from);

            var captured = new List<TokenRef>();
            var move = new Move(0, 0, from, from + 3, 3);
            Rules.ApplyMove(g.State, config, move, captured);

            CollectionAssert.IsEmpty(captured, "a partner is never captured");
            Assert.AreNotEqual(Board.BaseProgress, g.State.GetProgress(2, 0));
            Assert.IsFalse(g.State.HasCaptured(0), "sharing a cell with your partner is not a capture");
        }

        [Test]
        public void LandingOnAnOpponentStillSendsItHome()
        {
            var g = Game();
            var config = Cfg();
            int cell = 20;
            T.Put(g, 1, 0, T.ProgressForCell(g.State.SeatOf(1), cell));      // an opponent is standing there
            int from = T.ProgressForCell(g.State.SeatOf(0), cell) - 3;
            T.Put(g, 0, 0, from);

            var captured = new List<TokenRef>();
            Rules.ApplyMove(g.State, config, new Move(0, 0, from, from + 3, 3), captured);

            Assert.AreEqual(1, captured.Count);
            Assert.AreEqual(1, captured[0].Player);
            Assert.AreEqual(Board.BaseProgress, g.State.GetProgress(1, 0));
        }

        // ---------- winning ----------

        [Test]
        public void OnePartnerHomeIsNotYetAWin()
        {
            var g = Game();
            Finish(g, 0);
            Assert.IsTrue(Rules.IsDone(g.State, 0), "player 0 personally finished");
            Assert.IsFalse(Rules.HasWon(g.State, Cfg(), 0), "the side still needs player 2");
        }

        [Test]
        public void BothPartnersHomeWinsForBothOfThem()
        {
            var g = Game();
            Finish(g, 0);
            Finish(g, 2);
            Assert.IsTrue(Rules.HasWon(g.State, Cfg(), 0));
            Assert.IsTrue(Rules.HasWon(g.State, Cfg(), 2), "the win belongs to the side, not to one seat");
            Assert.IsFalse(Rules.HasWon(g.State, Cfg(), 1));
        }

        [Test]
        public void ClassicStillWinsOnYourOwnFourPawns()
        {
            var g = T.Game(4, new ScriptedDice(), new RulesConfig());
            Finish(g, 0);
            Assert.IsTrue(Rules.HasWon(g.State, new RulesConfig(), 0));
        }

        // ---------- turn order ----------

        [Test]
        public void AFinishedPartnerIsSkipped()
        {
            var g = Game(new ScriptedDice(1, 1, 1));
            Finish(g, 1);                                                    // player 1 has nothing left to move
            T.Put(g, 0, 0, 10);
            g.Roll();
            g.Play(g.LegalMoves.First());
            Assert.AreEqual(2, g.CurrentPlayer, "the dice skips the finished player 1 and goes to player 2");
        }

        [Test]
        public void AFinishedPlayerIsNotSkippedOutsideTeamUp()
        {
            var g = T.Game(4, new ScriptedDice(1, 1, 1), new RulesConfig());
            for (int t = 1; t < Board.TokensPerPlayer; t++) T.Put(g, 1, t, Board.FinishProgress);
            T.Put(g, 0, 0, 10);
            g.Roll();
            g.Play(g.LegalMoves.First());
            Assert.AreEqual(1, g.CurrentPlayer, "classic turn order never skips anybody");
        }

        // ---------- a whole game ----------

        [Test]
        public void RandomTeamGamesAlwaysFinishWithBothPartnersHome()
        {
            for (int seed = 0; seed < 25; seed++)
            {
                var rng = new Random(seed);
                var g = new LudoGame(Board.DefaultSeats(4), Cfg(), new RandomDiceAdapter(rng));
                int winner = -1;
                g.GameWon += p => winner = p;
                for (int step = 0; step < 20000 && winner < 0; step++)
                {
                    var roll = g.Roll();
                    while (g.Phase == TurnPhase.WaitingForMove)
                        g.Play(g.LegalMoves[rng.Next(g.LegalMoves.Count)]);
                }
                Assert.GreaterOrEqual(winner, 0, "seed " + seed + " never finished");
                int partner = winner == 0 ? 2 : winner == 2 ? 0 : winner == 1 ? 3 : 1;
                Assert.IsTrue(Rules.IsDone(g.State, winner) && Rules.IsDone(g.State, partner),
                    "seed " + seed + ": the game ended before both partners were home");
            }
        }

        /// <summary>A plain random dice, seeded so a failing game can be replayed.</summary>
        sealed class RandomDiceAdapter : IDiceSource
        {
            readonly Random rng;
            public RandomDiceAdapter(Random rng) { this.rng = rng; }
            public int Next() => rng.Next(1, 7);
        }
    }
}
