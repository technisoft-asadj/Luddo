using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    public class MatchmakingRulesTests
    {
        static readonly MatchmakingSettings S = new MatchmakingSettings();     // 20 s full-table wait, 60 s timeout, min 2

        [Test]
        public void AFullTableAlwaysStartsImmediately()
        {
            Assert.IsTrue(MatchmakingRules.ShouldStart(4, 4, ranked: false, searchSeconds: 0f, S));
            Assert.IsTrue(MatchmakingRules.ShouldStart(2, 2, ranked: true, searchSeconds: 0f, S));
        }

        [Test]
        public void AQuickTableStartsSmallerOnlyAfterTheWait()
        {
            Assert.IsFalse(MatchmakingRules.ShouldStart(3, 4, false, 19.9f, S));
            Assert.IsTrue(MatchmakingRules.ShouldStart(3, 4, false, 20f, S));
            Assert.IsTrue(MatchmakingRules.ShouldStart(2, 4, false, 25f, S));
        }

        [Test]
        public void NeverStartsWithOnePersonAlone()
        {
            Assert.IsFalse(MatchmakingRules.ShouldStart(1, 4, false, 59f, S));
            Assert.IsFalse(MatchmakingRules.ShouldStart(1, 2, false, 100f, S));
        }

        [Test]
        public void ARankedTableMustBeExactlyFull()
        {
            Assert.IsFalse(MatchmakingRules.ShouldStart(3, 4, ranked: true, searchSeconds: 50f, S));
            Assert.IsTrue(MatchmakingRules.ShouldStart(4, 4, ranked: true, searchSeconds: 50f, S));
        }

        [Test]
        public void TheSearchTimesOut()
        {
            Assert.IsFalse(MatchmakingRules.TimedOut(59.9f, S));
            Assert.IsTrue(MatchmakingRules.TimedOut(60f, S));
        }

        [Test]
        public void ALoneHostMovesToARoomWithMorePeople()
        {
            Assert.IsTrue(MatchmakingRules.ShouldMerge(1, "bbb", 2, "zzz"));
            Assert.IsTrue(MatchmakingRules.ShouldMerge(1, "bbb", 3, "aaa"));
        }

        [Test]
        public void EqualRoomsMergeIntoTheSmallerIdOnly()
        {
            // two lone hosts see each other: exactly one of them moves, so they never swap places or both stay
            bool aMoves = MatchmakingRules.ShouldMerge(1, "aaa", 1, "bbb");
            bool bMoves = MatchmakingRules.ShouldMerge(1, "bbb", 1, "aaa");
            Assert.IsFalse(aMoves);
            Assert.IsTrue(bMoves);
        }

        [Test]
        public void ARoomWithSeveralPeopleNeverMoves()
        {
            Assert.IsFalse(MatchmakingRules.ShouldMerge(2, "bbb", 3, "aaa"));
            Assert.IsFalse(MatchmakingRules.ShouldMerge(1, "bbb", 0, "aaa"));
            Assert.IsFalse(MatchmakingRules.ShouldMerge(1, "bbb", 1, "bbb"));    // never itself
            Assert.IsFalse(MatchmakingRules.ShouldMerge(1, "bbb", 1, ""));
        }

        [Test]
        public void ThePhaseTextFollowsTheClock()
        {
            StringAssert.Contains("Searching", MatchmakingRules.Phase(3f, S));
            StringAssert.Contains("widening", MatchmakingRules.Phase(30f, S));
            StringAssert.Contains("Not enough", MatchmakingRules.Phase(61f, S));
        }
    }
}
