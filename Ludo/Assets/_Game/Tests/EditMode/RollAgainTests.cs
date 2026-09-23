using System.Linq;
using NUnit.Framework;
using Ludo.AI;
using Ludo.Core;

namespace Ludo.Tests
{
    /// <summary>Ludo Star turn: a 6 = roll again first; then every number is played, in the order the player chooses.</summary>
    public class RollAgainTests
    {
        static RulesConfig Cfg(GameMode mode = GameMode.Classic)
        {
            var c = new RulesConfig().WithMode(mode);
            c.RollAgainOnSix = true;
            return c;
        }

        [Test]
        public void SixMeansRollAgainBeforeMoving()
        {
            var g = T.Game(2, new ScriptedDice(6, 5), Cfg());
            var first = g.Roll();
            Assert.IsTrue(first.RollAgain);
            Assert.AreEqual(0, first.LegalMoves.Length);
            Assert.AreEqual(TurnPhase.WaitingForRoll, g.Phase);
            Assert.AreEqual(0, g.CurrentPlayer);
            var second = g.Roll();
            Assert.IsFalse(second.RollAgain);
            CollectionAssert.AreEqual(new[] { 6, 5 }, second.Pending);
            Assert.IsTrue(second.LegalMoves.All(m => m.Roll == 6), "all pawns in base: only the 6 can bring one out");
        }

        [Test]
        public void PlayerChoosesWhichNumberFirst()
        {
            var g = T.Game(2, new ScriptedDice(6, 5), Cfg());
            T.Put(g, 0, 0, 10);
            g.Roll();
            var roll = g.Roll();
            // pawn 0 can use 6 or 5; the base pawns only the 6
            Assert.IsTrue(roll.LegalMoves.Any(m => m.Token == 0 && m.Roll == 6));
            Assert.IsTrue(roll.LegalMoves.Any(m => m.Token == 0 && m.Roll == 5));
            var five = roll.LegalMoves.First(m => m.Token == 0 && m.Roll == 5);
            var r1 = g.Play(five);                                    // the 5 first
            Assert.IsTrue(r1.MoreMoves);
            Assert.AreEqual(TurnPhase.WaitingForMove, g.Phase);
            Assert.AreEqual(0, g.CurrentPlayer);
            CollectionAssert.AreEqual(new[] { 6 }, g.PendingRolls.ToArray());
            Assert.IsTrue(g.LegalMoves.All(m => m.Roll == 6));
            var r2 = g.Play(g.LegalMoves.First(m => m.Token == 0));  // then the 6 with the same pawn
            Assert.IsFalse(r2.MoreMoves);
            Assert.AreEqual(21, g.State.GetProgress(0, 0));
            Assert.AreEqual(1, g.CurrentPlayer, "no capture, no home: the turn passes");
        }

        [Test]
        public void ThreeSixesLoseTheWholeTurn()
        {
            var g = T.Game(2, new ScriptedDice(6, 6, 6), Cfg());
            T.Put(g, 0, 0, 10);
            Assert.IsTrue(g.Roll().RollAgain);
            Assert.IsTrue(g.Roll().RollAgain);
            var third = g.Roll();
            Assert.AreEqual(PassReason.ThreeSixes, third.Pass);
            Assert.AreEqual(1, g.CurrentPlayer);
            Assert.AreEqual(10, g.State.GetProgress(0, 0), "nothing moved");
        }

        [Test]
        public void NumbersCanChainOnThePawnThatCameOut()
        {
            var g = T.Game(2, new ScriptedDice(6, 3), Cfg());
            g.Roll();
            var roll = g.Roll();                                      // 6 + 3, all pawns in base: bring one out with the 6
            var r = g.Play(roll.LegalMoves[0]);
            Assert.IsTrue(r.MoreMoves, "the 3 can now move the pawn that came out");
            g.Play(g.LegalMoves[0]);
            Assert.AreEqual(3, g.State.GetProgress(0, roll.LegalMoves[0].Token));
            Assert.AreEqual(1, g.CurrentPlayer);
        }

        [Test]
        public void CaptureEarnsOneRollAfterAllNumbers()
        {
            var g = T.Game(2, new ScriptedDice(2, 4), Cfg());
            T.Put(g, 0, 0, 10);
            T.Put(g, 1, 0, T.ProgressForCell(g.State.SeatOf(1), 12));
            var roll = g.Roll();
            var r = g.Play(roll.LegalMoves.First(m => m.Token == 0));
            Assert.AreEqual(1, r.Captured.Length);
            Assert.IsTrue(r.ExtraTurn);
            Assert.AreEqual(0, g.CurrentPlayer);
            Assert.AreEqual(TurnPhase.WaitingForRoll, g.Phase);
        }

        [TestCase(GameMode.Classic, 4)]
        [TestCase(GameMode.Master, 3)]
        [TestCase(GameMode.Arrow, 2)]
        [TestCase(GameMode.Blitz, 4)]
        public void RandomGamesFinishAndReplayIdentically(GameMode mode, int players)
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                var cfg = Cfg(mode);
                var dice = new QueuedDiceSource();
                var host = new LudoGame(Board.DefaultSeats(players), cfg, dice);
                var random = new RandomDiceSource(seed);
                var ai = new MediumAi(seed);
                var story = new System.Collections.Generic.List<int>();
                int guard = 0;
                while (host.Phase != TurnPhase.GameOver && guard++ < 30000)
                {
                    if (host.Phase == TurnPhase.WaitingForRoll)
                    {
                        int v = random.Next();
                        dice.Push(v); story.Add(v);
                        host.Roll();
                        continue;
                    }
                    var moves = host.LegalMoves.ToArray();
                    var m = ai.Choose(host.State, cfg, moves);
                    if (!MatchReplay.OnlyOneChoice(moves)) story.Add(MatchReplay.PickBase + System.Array.IndexOf(moves, m));
                    else m = moves[0];
                    host.Play(m);
                }
                Assert.AreEqual(TurnPhase.GameOver, host.Phase, mode + " seed " + seed);

                // a phone that was away replays the story and ends in the same position
                var qd = new QueuedDiceSource();
                var copy = new LudoGame(Board.DefaultSeats(players), cfg, qd);
                MatchReplay.Apply(copy, qd, story);
                for (int p = 0; p < players; p++)
                    for (int t = 0; t < 4; t++)
                        Assert.AreEqual(host.State.GetProgress(p, t), copy.State.GetProgress(p, t));
                Assert.AreEqual(host.State.Winner, copy.State.Winner);
            }
        }
    }
}
