using System;
using NUnit.Framework;
using Ludo.Core;
using Ludo.Game;
using Ludo.Online;

namespace Ludo.Tests
{
    /// <summary>
    /// The online design in one sentence: every phone runs the same engine, the host announces "the die shows N" and
    /// "play move number K", and all phones stay identical. These tests prove that claim without any network.
    /// </summary>
    public class OnlineSyncTests
    {
        static LudoGame NewGame(int players, IDiceSource dice) =>
            new LudoGame(Board.DefaultSeats(players), new RulesConfig(), dice);

        [Test]
        public void QueuedDiceHandsOutExactlyWhatWasPushed()
        {
            var q = new QueuedDiceSource();
            q.Push(6); q.Push(1); q.Push(4);
            Assert.AreEqual(6, q.Next());
            Assert.AreEqual(1, q.Next());
            Assert.AreEqual(4, q.Next());
            Assert.Throws<InvalidOperationException>(() => q.Next());
        }

        [Test]
        public void QueuedDiceRefusesImpossibleValues()
        {
            var q = new QueuedDiceSource();
            Assert.Throws<ArgumentOutOfRangeException>(() => q.Push(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => q.Push(7));
        }

        [TestCase(2, 11)]
        [TestCase(3, 22)]
        [TestCase(4, 33)]
        [TestCase(4, 44)]
        public void HostAndGuestEnginesStayIdentical(int players, int seed)
        {
            var hostDice = new QueuedDiceSource();
            var guestDice = new QueuedDiceSource();
            var host = NewGame(players, hostDice);
            var guest = NewGame(players, guestDice);
            var random = new RandomDiceSource(seed);
            var picker = new Random(seed);

            for (int turn = 0; turn < 4000 && host.Phase != TurnPhase.GameOver; turn++)
            {
                // the host decides the die and announces it: both engines receive the same value
                int value = random.Next();
                hostDice.Push(value);
                guestDice.Push(value);
                RollResult a = host.Roll();
                RollResult b = guest.Roll();
                Assert.AreEqual(a.Value, b.Value);
                Assert.AreEqual(a.Pass, b.Pass);
                Assert.AreEqual(a.LegalMoves.Length, b.LegalMoves.Length);
                if (a.Pass != PassReason.None) { AssertSame(host, guest, players); continue; }

                // the host announces "play move number K"; the guest plays the move at the same index
                int index = picker.Next(a.LegalMoves.Length);
                MoveResult ra = host.Play(a.LegalMoves[index]);
                MoveResult rb = guest.Play(b.LegalMoves[index]);
                Assert.AreEqual(ra.Captured.Length, rb.Captured.Length);
                Assert.AreEqual(ra.ExtraTurn, rb.ExtraTurn);
                AssertSame(host, guest, players);
            }
            Assert.AreEqual(host.Phase == TurnPhase.GameOver, guest.Phase == TurnPhase.GameOver);
        }

        static void AssertSame(LudoGame a, LudoGame b, int players)
        {
            Assert.AreEqual(a.CurrentPlayer, b.CurrentPlayer);
            Assert.AreEqual(a.Phase, b.Phase);
            for (int p = 0; p < players; p++)
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                    Assert.AreEqual(a.State.GetProgress(p, t), b.State.GetProgress(p, t), "player " + p + " token " + t);
        }

        [Test]
        public void SeatingPlanOnlyOffersMovesToTheSeatOwner()
        {
            // the host checks "does this sender own the seat whose turn it is" - the plan is what it checks against
            var link = new UnityEngine.GameObject("test-link").AddComponent<MatchLink>();
            try
            {
                var start = new MatchStart
                {
                    seats = new[]
                    {
                        new MatchSeat { playerId = "host", name = "H", ai = -1 },
                        new MatchSeat { playerId = "guest", name = "G", ai = -1 },
                        new MatchSeat { name = "CPU 2", ai = 1 }
                    }
                };
                link.SetSeatOwners(start);
                // nobody has asked for anything yet
                Assert.IsFalse(link.TryTakeRollRequest(0));
                Assert.IsFalse(link.TryTakeRollRequest(1));
                Assert.IsFalse(link.TryTakeRollRequest(2));      // a computer seat has no owner
                Assert.IsFalse(link.TryTakePickRequest(9, out _)); // a seat that does not exist
            }
            finally { UnityEngine.Object.DestroyImmediate(link.gameObject); }
        }

        [Test]
        public void MatchStartSurvivesTheWire()
        {
            var start = new MatchStart
            {
                seats = new[]
                {
                    new MatchSeat { playerId = "a", name = "Ahmad", avatar = 3, ai = -1 },
                    new MatchSeat { name = "CPU 1", ai = 2 }
                }
            };
            string json = UnityEngine.JsonUtility.ToJson(start);
            var back = UnityEngine.JsonUtility.FromJson<MatchStart>(json);
            Assert.AreEqual(2, back.seats.Length);
            Assert.AreEqual("Ahmad", back.seats[0].name);
            Assert.AreEqual(3, back.seats[0].avatar);
            Assert.AreEqual(-1, back.seats[0].ai);
            Assert.AreEqual(2, back.seats[1].ai);
        }

        [Test]
        public void CloudNamesFollowUnitysRules()
        {
            Assert.AreEqual("Ahmad_Ali", OnlineService.CloudNameFrom("Ahmad Ali"));
            Assert.AreEqual("Player", OnlineService.CloudNameFrom("   "));
            Assert.AreEqual(50, OnlineService.CloudNameFrom(new string('x', 80)).Length);
            Assert.AreEqual("Ahmad Ali", OnlineService.ShownName("Ahmad_Ali#4821"));
        }
    }
}
