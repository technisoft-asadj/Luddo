using System;
using NUnit.Framework;
using Ludo.Core;
using Ludo.Online;

namespace Ludo.Tests
{
    public class ProgressionTests
    {
        [Test]
        public void NewPlayerIsLevelOneWithNoXp()
        {
            Assert.AreEqual(1, Progression.LevelFor(0));
            Assert.AreEqual(0, Progression.XpIntoLevel(0));
            Assert.AreEqual(0f, Progression.Fraction(0));
        }

        [Test]
        public void LevelsCostMoreAndMore()
        {
            Assert.AreEqual(100, Progression.XpToNext(1));
            Assert.AreEqual(150, Progression.XpToNext(2));
            Assert.AreEqual(200, Progression.XpToNext(3));
            Assert.AreEqual(100, Progression.TotalXpFor(2));
            Assert.AreEqual(250, Progression.TotalXpFor(3));
            Assert.AreEqual(450, Progression.TotalXpFor(4));
        }

        [Test]
        public void LevelChangesExactlyAtTheThreshold()
        {
            Assert.AreEqual(1, Progression.LevelFor(99));
            Assert.AreEqual(2, Progression.LevelFor(100));
            Assert.AreEqual(2, Progression.LevelFor(249));
            Assert.AreEqual(3, Progression.LevelFor(250));
            Assert.AreEqual(50, Progression.XpIntoLevel(150));
            Assert.AreEqual(0.5f, Progression.Fraction(175), 1e-4f);
        }

        [Test]
        public void LevelAndXpAreConsistentForEveryLevel()
        {
            for (int level = 1; level < Progression.MaxLevel; level++)
            {
                long start = Progression.TotalXpFor(level);
                Assert.AreEqual(level, Progression.LevelFor(start), "start of level " + level);
                Assert.AreEqual(level, Progression.LevelFor(start + Progression.XpToNext(level) - 1), "end of level " + level);
            }
        }

        [Test]
        public void LevelStopsAtTheTop()
        {
            Assert.AreEqual(Progression.MaxLevel, Progression.LevelFor(long.MaxValue / 2));
            Assert.AreEqual(1f, Progression.Fraction(long.MaxValue / 2));
        }
    }
}
