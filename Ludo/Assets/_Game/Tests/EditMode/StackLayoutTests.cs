using NUnit.Framework;
using UnityEngine;
using Ludo.Game;

namespace Ludo.Tests
{
    public class StackLayoutTests
    {
        [Test]
        public void SinglePawnStaysInTheMiddleAtFullSize()
        {
            Assert.AreEqual(Vector2.zero, StackLayout.Offset(0, 1));
            Assert.AreEqual(1f, StackLayout.Scale(1));
        }

        [Test]
        public void EveryPawnInAStackGetsItsOwnSpot()
        {
            for (int count = 2; count <= 16; count++)
                for (int a = 0; a < count; a++)
                    for (int b = a + 1; b < count; b++)
                        Assert.Greater((StackLayout.Offset(a, count) - StackLayout.Offset(b, count)).magnitude, 0.2f,
                            "pawns " + a + " and " + b + " of " + count + " overlap");
        }

        [Test]
        public void StackIsCentredOnTheCell()
        {
            for (int count = 2; count <= 9; count++)
            {
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < count; i++) sum += StackLayout.Offset(i, count);
                Assert.Less((sum / count).magnitude, 0.08f, "stack of " + count + " leans to one side");
            }
        }

        [Test]
        public void StackedPawnsShrinkButStayReadable()
        {
            float previous = 1f;
            for (int count = 2; count <= 9; count++)
            {
                float s = StackLayout.Scale(count);
                Assert.LessOrEqual(s, previous);
                Assert.GreaterOrEqual(s, 0.5f);
                previous = s;
            }
        }

        [Test]
        public void FourPawnsMakeATwoByTwoBlockInsideTheCell()
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 o = StackLayout.Offset(i, 4);
                Assert.LessOrEqual(Mathf.Abs(o.x), 0.5f);
                Assert.LessOrEqual(Mathf.Abs(o.y), 0.5f);
            }
            Assert.AreNotEqual(StackLayout.Offset(0, 4).y, StackLayout.Offset(3, 4).y);
        }
    }
}
