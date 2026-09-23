using System.Collections.Generic;
using NUnit.Framework;
using Ludo.AI;
using Ludo.Core;

namespace Ludo.Tests
{
    public class AiTests
    {
        // Two players: player 0 = Red (start cell 0), player 1 = Yellow (start cell 26).
        static GameState NewState() => new GameState(Board.DefaultSeats(2));

        static List<Move> Legal(GameState s, int player, int roll)
        {
            var moves = new List<Move>();
            Rules.GetLegalMoves(s, new RulesConfig(), player, roll, moves);
            return moves;
        }

        // ---------- every AI only ever plays a legal move ----------

        [TestCase(AiDifficulty.Easy)]
        [TestCase(AiDifficulty.Medium)]
        [TestCase(AiDifficulty.Hard)]
        public void Ai_AlwaysChoosesALegalMove_AndDoesNotChangeTheState(AiDifficulty level)
        {
            for (int players = 2; players <= 4; players++)
                for (int seed = 0; seed < 15; seed++)
                {
                    var cfg = new RulesConfig();
                    var game = new LudoGame(Board.DefaultSeats(players), cfg, new RandomDiceSource(seed));
                    var ai = AiFactory.Create(level, seed);
                    int rolls = 0;
                    while (game.Phase != TurnPhase.GameOver && rolls++ < 100000)
                    {
                        var roll = game.Roll();
                        if (roll.Pass != PassReason.None) continue;

                        var snapshot = game.State.Clone();
                        var pick = ai.Choose(game.State, cfg, roll.LegalMoves);
                        CollectionAssert.Contains(roll.LegalMoves, pick, level + " chose an illegal move");
                        for (int p = 0; p < players; p++)
                            for (int t = 0; t < Board.TokensPerPlayer; t++)
                                Assert.AreEqual(snapshot.GetProgress(p, t), game.State.GetProgress(p, t), "AI modified the state");
                        game.Play(pick);
                    }
                    Assert.AreEqual(TurnPhase.GameOver, game.Phase, level + " game did not finish (players=" + players + " seed=" + seed + ")");
                }
        }

        [Test]
        public void Ai_IsDeterministic_ForTheSameSeed()
        {
            var cfg = new RulesConfig();
            var s = NewState();
            T.Put(s, 0, 0, 5); T.Put(s, 0, 1, 20); T.Put(s, 0, 2, 33);
            var legal = Legal(s, 0, 2);
            foreach (AiDifficulty level in System.Enum.GetValues(typeof(AiDifficulty)))
                Assert.AreEqual(AiFactory.Create(level, 7).Choose(s, cfg, legal), AiFactory.Create(level, 7).Choose(s, cfg, legal), level.ToString());
        }

        // ---------- what the smarter AIs care about ----------

        [Test]
        public void Medium_And_Hard_TakeACapture()
        {
            var cfg = new RulesConfig();
            var s = NewState();
            T.Put(s, 0, 0, 5);                                   // Red on cell 5, an enemy sits on cell 6
            T.Put(s, 0, 1, 20);
            T.Put(s, 1, 0, T.ProgressForCell(2, 6));
            var legal = Legal(s, 0, 1);

            for (int seed = 0; seed < 20; seed++)
            {
                Assert.AreEqual(0, AiFactory.Create(AiDifficulty.Medium, seed).Choose(s, cfg, legal).Token, "medium seed " + seed);
                Assert.AreEqual(0, AiFactory.Create(AiDifficulty.Hard, seed).Choose(s, cfg, legal).Token, "hard seed " + seed);
            }
        }

        [Test]
        public void Medium_And_Hard_FinishATokenWhenTheyCan()
        {
            var cfg = new RulesConfig();
            var s = NewState();
            T.Put(s, 0, 0, 53);                                  // 3 steps from home
            T.Put(s, 0, 1, 10);
            var legal = Legal(s, 0, 3);

            for (int seed = 0; seed < 20; seed++)
            {
                Assert.AreEqual(0, AiFactory.Create(AiDifficulty.Medium, seed).Choose(s, cfg, legal).Token);
                Assert.AreEqual(0, AiFactory.Create(AiDifficulty.Hard, seed).Choose(s, cfg, legal).Token);
            }
        }

        [Test]
        public void Medium_And_Hard_LeaveBaseWithASix_WhenNothingBetterExists()
        {
            var cfg = new RulesConfig();
            var s = NewState();
            T.Put(s, 0, 0, 10);                                  // one token on the track, three in base
            var legal = Legal(s, 0, 6);

            for (int seed = 0; seed < 20; seed++)
            {
                Assert.AreEqual(Board.BaseProgress, AiFactory.Create(AiDifficulty.Medium, seed).Choose(s, cfg, legal).From);
                Assert.AreEqual(Board.BaseProgress, AiFactory.Create(AiDifficulty.Hard, seed).Choose(s, cfg, legal).From);
            }
        }

        [Test]
        public void Hard_RescuesATokenThatAnEnemyCanCapture()
        {
            var cfg = new RulesConfig();
            var s = NewState();
            T.Put(s, 0, 0, 5);                                   // Red on cell 5 (unsafe) ...
            T.Put(s, 0, 1, 10);                                  // ... and on cell 10
            T.Put(s, 1, 0, T.ProgressForCell(2, 2));             // Yellow on cell 2: 3 cells behind Red's first token
            var legal = Legal(s, 0, 3);                          // token 0 -> cell 8 (safe star), token 1 -> cell 13 (safe)

            for (int seed = 0; seed < 20; seed++)
                Assert.AreEqual(0, AiFactory.Create(AiDifficulty.Hard, seed).Choose(s, cfg, legal).Token,
                    "Hard should move the token that is in danger (seed " + seed + ")");
        }

        [Test]
        public void Danger_IsHighForAnExposedToken_AndZeroWhenSafe()
        {
            var exposed = NewState();
            T.Put(exposed, 0, 0, 5);                             // Red on cell 5, unsafe
            T.Put(exposed, 1, 0, T.ProgressForCell(2, 2));       // enemy 3 cells behind it
            Assert.Greater(HardAi.Danger(exposed, 0), 0.0);

            var onStar = NewState();
            T.Put(onStar, 0, 0, 8);                              // cell 8 is a star (safe)
            T.Put(onStar, 1, 0, T.ProgressForCell(2, 5));
            Assert.AreEqual(0.0, HardAi.Danger(onStar, 0), 1e-9);

            var farAway = NewState();
            T.Put(farAway, 0, 0, 5);
            T.Put(farAway, 1, 0, T.ProgressForCell(2, 40));      // enemy is more than 6 cells behind
            Assert.AreEqual(0.0, HardAi.Danger(farAway, 0), 1e-9);

            var inBase = NewState();
            T.Put(inBase, 1, 0, T.ProgressForCell(2, 0));
            Assert.AreEqual(0.0, HardAi.Danger(inBase, 0), 1e-9);
        }

        [Test]
        public void Threat_IsHighWhenAnEnemyIsWithinOneRoll()
        {
            var s = NewState();
            T.Put(s, 0, 0, 5);                                   // Red on cell 5
            T.Put(s, 1, 0, T.ProgressForCell(2, 9));             // unsafe enemy 4 cells ahead
            Assert.Greater(HardAi.Threat(s, 0), 0.0);
            Assert.Greater(HardAi.Danger(s, 1), 0.0);            // seen from the enemy's side it is danger
        }

        [Test]
        public void Hard_EscapesDanger_WhereMediumDoesNot()
        {
            var cfg = new RulesConfig();
            var s = NewState();
            T.Put(s, 0, 0, 20);                                  // Red token 0 on cell 20 ...
            T.Put(s, 0, 1, 29);                                  // ... token 1 on cell 29
            T.Put(s, 1, 0, T.ProgressForCell(2, 17));            // Yellow on cell 17: 3 cells behind token 0 (danger!)
            var legal = Legal(s, 0, 4);                          // token 0 -> cell 24 (out of reach), token 1 -> cell 33

            for (int seed = 0; seed < 20; seed++)
            {
                Assert.AreEqual(0, AiFactory.Create(AiDifficulty.Hard, seed).Choose(s, cfg, legal).Token,
                    "Hard should run away with the threatened token (seed " + seed + ")");
                Assert.AreEqual(1, AiFactory.Create(AiDifficulty.Medium, seed).Choose(s, cfg, legal).Token,
                    "Medium only looks at its own move, not at the danger (seed " + seed + ")");
            }
        }

        // ---------- strength: better AIs should win more often ----------

        /// <summary>Win rate of A against B in a 2-player game, alternating who moves first.</summary>
        static double WinRate(AiDifficulty a, AiDifficulty b, int games)
        {
            int aWins = 0;
            var cfg = new RulesConfig();
            for (int g = 0; g < games; g++)
            {
                bool aFirst = g % 2 == 0;
                var game = new LudoGame(Board.DefaultSeats(2), cfg, new RandomDiceSource(5000 + g));
                var players = new[] { AiFactory.Create(aFirst ? a : b, g), AiFactory.Create(aFirst ? b : a, g + 999) };
                int rolls = 0;
                while (game.Phase != TurnPhase.GameOver && rolls++ < 100000)
                {
                    var roll = game.Roll();
                    if (roll.Pass != PassReason.None) continue;
                    game.Play(players[game.CurrentPlayer].Choose(game.State, cfg, roll.LegalMoves));
                }
                Assert.AreEqual(TurnPhase.GameOver, game.Phase, "game did not finish");
                bool winnerIsFirst = game.State.Winner == 0;
                if (winnerIsFirst == aFirst) aWins++;
            }
            return aWins / (double)games;
        }

        [Test]
        public void Medium_BeatsEasy_Clearly()
        {
            double rate = WinRate(AiDifficulty.Medium, AiDifficulty.Easy, 400);
            TestContext.Out.WriteLine("Medium vs Easy win rate: " + rate);
            Assert.Greater(rate, 0.75);
        }

        [Test]
        public void Hard_BeatsEasy_Clearly()
        {
            double rate = WinRate(AiDifficulty.Hard, AiDifficulty.Easy, 400);
            TestContext.Out.WriteLine("Hard vs Easy win rate: " + rate);
            Assert.Greater(rate, 0.75);
        }

        [Test]
        public void Hard_BeatsMedium()
        {
            double rate = WinRate(AiDifficulty.Hard, AiDifficulty.Medium, 1200);
            TestContext.Out.WriteLine("Hard vs Medium win rate: " + rate);
            Assert.Greater(rate, 0.54);
        }
    }
}
