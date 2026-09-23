using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ludo.AI;
using Ludo.Core;

namespace Ludo.Tests
{
    /// <summary>The game modes (Classic / Master / Arrow / Blitz): every special rule, and full games that always finish.</summary>
    public class GameModeTests
    {
        static RulesConfig Cfg(GameMode mode) => new RulesConfig().WithMode(mode);

        static List<Move> Legal(GameState s, RulesConfig c, int player, int roll)
        {
            var list = new List<Move>();
            Rules.GetLegalMoves(s, c, player, roll, list);
            return list;
        }

        // ---------- Master ----------

        [Test]
        public void MasterPawnWithoutCaptureGoesRoundAgain()
        {
            var g = T.Game(2, new ScriptedDice(), Cfg(GameMode.Master));
            T.Put(g, 0, 0, 48);
            var m = Legal(g.State, Cfg(GameMode.Master), 0, 4).Single(x => x.Token == 0);
            Assert.AreEqual(0, m.To, "48 + 4 passes the entrance (50), the lap cell (57) and lands on its start cell");
            m = Legal(g.State, Cfg(GameMode.Master), 0, 3).Single(x => x.Token == 0);
            Assert.AreEqual(Board.LapProgress, m.To);
        }

        [Test]
        public void MasterLapCellIsTheCellBeforeTheStartCell()
        {
            for (int seat = 0; seat < 4; seat++)
                Assert.AreEqual((Board.StartCell(seat) + 51) % 52, Board.ToOuterCell(seat, Board.LapProgress));
            var g = T.Game(2, new ScriptedDice(), Cfg(GameMode.Master));
            T.Put(g, 0, 0, Board.LapProgress);
            Assert.AreEqual(2, Legal(g.State, Cfg(GameMode.Master), 0, 3).Single(x => x.Token == 0).To);
        }

        [Test]
        public void MasterCaptureOpensTheHomePath()
        {
            var cfg = Cfg(GameMode.Master);
            var s = new GameState(Board.DefaultSeats(2));
            T.Put(s, 0, 0, 10);
            T.Put(s, 0, 1, 48);
            T.Put(s, 1, 0, T.ProgressForCell(s.SeatOf(1), 12));                // an enemy on cell 12 (not safe)
            Assert.AreEqual(0, Legal(s, cfg, 0, 4).Single(x => x.Token == 1).To, "before any capture: round again");
            var capture = Legal(s, cfg, 0, 2).Single(x => x.Token == 0);
            var caught = new List<TokenRef>();
            Rules.ApplyMove(s, cfg, capture, caught);
            Assert.AreEqual(1, caught.Count);
            Assert.IsTrue(s.HasCaptured(0));
            Assert.AreEqual(52, Legal(s, cfg, 0, 4).Single(x => x.Token == 1).To, "after a capture: into the home path");
        }

        [Test]
        public void ClassicIgnoresCaptures()
        {
            var s = new GameState(Board.DefaultSeats(2));
            T.Put(s, 0, 1, 48);
            Assert.AreEqual(52, Legal(s, new RulesConfig(), 0, 4).Single(x => x.Token == 1).To);
        }

        // ---------- Arrow ----------

        [Test]
        public void ArrowCellSlidesSixForward()
        {
            var cfg = Cfg(GameMode.Arrow);
            var s = new GameState(Board.DefaultSeats(2));
            T.Put(s, 0, 0, 0);
            var m = Legal(s, cfg, 0, 2).Single(x => x.Token == 0);            // lands on cell 2 = an arrow
            Assert.AreEqual(8, m.To);
            Assert.IsTrue(Board.IsSafeCell(Board.ToOuterCell(0, m.To)), "the arrow ends on the star cell");
            Assert.AreEqual(2, Legal(s, new RulesConfig(), 0, 2).Single(x => x.Token == 0).To, "no arrows in Classic");
        }

        [Test]
        public void EveryArrowIsReachableAndNeverPassesTheOwnEntrance()
        {
            for (int seat = 0; seat < 4; seat++)
                for (int progress = 0; progress <= Board.LastOuterProgress; progress++)
                {
                    if (!Board.IsArrowCell(Board.ToOuterCell(seat, progress))) continue;
                    Assert.LessOrEqual(progress + Board.ArrowJump, Board.LastOuterProgress);
                }
        }

        [Test]
        public void ArrowPathEndsWithTheSlide()
        {
            var steps = new List<int>();
            Rules.Path(new Move(0, 0, 0, 8, 2), steps);
            CollectionAssert.AreEqual(new[] { 1, 2, 8 }, steps);
        }

        // ---------- Blitz ----------

        [Test]
        public void BlitzStartsOnTheStartCellAndOnePawnHomeWins()
        {
            var cfg = Cfg(GameMode.Blitz);
            var g = T.Game(2, new ScriptedDice(5), cfg);
            for (int p = 0; p < 2; p++)
                for (int t = 0; t < 4; t++)
                    Assert.AreEqual(Board.StartProgress, g.State.GetProgress(p, t));
            T.Put(g, 0, 0, 51);
            var roll = g.Roll();
            var result = g.Play(roll.LegalMoves.First(x => x.Token == 0));
            Assert.IsTrue(result.GameWon);
            Assert.AreEqual(0, g.State.Winner);
        }

        // ---------- paths ----------

        [Test]
        public void PathGoesRoundTheLapCell()
        {
            var steps = new List<int>();
            Rules.Path(new Move(0, 0, 48, 1, 5), steps);
            CollectionAssert.AreEqual(new[] { 49, 50, Board.LapProgress, 0, 1 }, steps);
            Rules.Path(new Move(0, 0, 48, 53, 5), steps);
            CollectionAssert.AreEqual(new[] { 49, 50, 51, 52, 53 }, steps);
            Rules.Path(new Move(0, 0, Board.BaseProgress, 0, 6), steps);
            CollectionAssert.AreEqual(new[] { 0 }, steps);
        }

        // ---------- whole games ----------

        [TestCase(GameMode.Master, 2)]
        [TestCase(GameMode.Master, 4)]
        [TestCase(GameMode.Arrow, 3)]
        [TestCase(GameMode.Arrow, 4)]
        [TestCase(GameMode.Blitz, 2)]
        [TestCase(GameMode.Blitz, 4)]
        public void RandomGamesInEveryModeFinish(GameMode mode, int players)
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var cfg = Cfg(mode);
                var g = new LudoGame(Board.DefaultSeats(players), cfg, new RandomDiceSource(seed));
                var ai = new MediumAi(seed);
                int guard = 0;
                while (g.Phase != TurnPhase.GameOver && guard++ < 20000)
                {
                    var roll = g.Roll();
                    if (roll.Pass != PassReason.None) continue;
                    var move = ai.Choose(g.State, cfg, roll.LegalMoves);
                    Assert.Contains(move, roll.LegalMoves);
                    g.Play(move);
                    for (int p = 0; p < players; p++)
                        for (int t = 0; t < 4; t++)
                        {
                            int v = g.State.GetProgress(p, t);
                            Assert.IsTrue(v == Board.BaseProgress || (v >= 0 && v <= Board.FinishProgress) || v == Board.LapProgress, "bad progress " + v);
                        }
                }
                Assert.AreEqual(TurnPhase.GameOver, g.Phase, mode + " seed " + seed + " did not finish");
            }
        }

        [TestCase(GameMode.Master)]
        [TestCase(GameMode.Arrow)]
        [TestCase(GameMode.Blitz)]
        public void HostAndGuestStayIdenticalInEveryMode(GameMode mode)
        {
            var cfg = Cfg(mode);
            var hostDice = new QueuedDiceSource();
            var guestDice = new QueuedDiceSource();
            var host = new LudoGame(Board.DefaultSeats(4), cfg, hostDice);
            var guest = new LudoGame(Board.DefaultSeats(4), cfg, guestDice);
            var random = new RandomDiceSource(99);
            var pick = new System.Random(99);
            for (int turn = 0; turn < 3000 && host.Phase != TurnPhase.GameOver; turn++)
            {
                int v = random.Next();
                hostDice.Push(v); guestDice.Push(v);
                var a = host.Roll(); var b = guest.Roll();
                Assert.AreEqual(a.LegalMoves.Length, b.LegalMoves.Length);
                if (a.Pass != PassReason.None) continue;
                int i = pick.Next(a.LegalMoves.Length);
                host.Play(a.LegalMoves[i]); guest.Play(b.LegalMoves[i]);
                for (int p = 0; p < 4; p++)
                    for (int t = 0; t < 4; t++)
                        Assert.AreEqual(host.State.GetProgress(p, t), guest.State.GetProgress(p, t));
            }
        }
    }
}
