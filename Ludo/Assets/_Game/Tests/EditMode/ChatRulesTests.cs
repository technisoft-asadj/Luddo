using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    public class ChatRulesTests
    {
        [Test]
        public void EmptyAndBlankMessagesAreDropped()
        {
            Assert.IsNull(ChatRules.Clean(null));
            Assert.IsNull(ChatRules.Clean(""));
            Assert.IsNull(ChatRules.Clean("    \n\t  "));
            Assert.IsNull(ChatRules.Clean("|||"));
        }

        [Test]
        public void MessagesAreTidied()
        {
            Assert.AreEqual("hello there", ChatRules.Clean("   hello    there \n"));
            Assert.AreEqual("a b", ChatRules.Clean("a|b".Replace("|", " ")));
            Assert.AreEqual("ab", ChatRules.Clean("a|b"));             // the wire separator never gets through
        }

        [Test]
        public void LongMessagesAreCut()
        {
            string s = ChatRules.Clean(new string('x', 500));
            Assert.AreEqual(ChatRules.MaxLength, s.Length);
        }

        [Test]
        public void RudeWordsAreMaskedInAnyCase()
        {
            Assert.AreEqual("you **** loser", ChatRules.MaskRude("you shit loser"));
            Assert.AreEqual("****", ChatRules.MaskRude("FUCK"));
            Assert.AreEqual("good game", ChatRules.MaskRude("good game"));
        }

        [Test]
        public void PrepareCleansAndMasks()
        {
            Assert.AreEqual("well **** played", ChatRules.Prepare("  well  fuck  played "));
            Assert.IsNull(ChatRules.Prepare("   "));
        }

        [Test]
        public void NamesAreLimitedAndNeverEmpty()
        {
            Assert.AreEqual("Player", ChatRules.CleanName(""));
            Assert.AreEqual(ChatRules.MaxNameLength, ChatRules.CleanName(new string('n', 60)).Length);
            Assert.AreEqual("Ab", ChatRules.CleanName("A|b"));
        }

        [Test]
        public void SpamIsThrottled()
        {
            var t = new ChatThrottle();
            Assert.IsTrue(t.Allow(10.0));
            Assert.IsFalse(t.Allow(10.5));
            Assert.IsFalse(t.Allow(11.4));
            Assert.IsTrue(t.Allow(11.6));
        }
    }
}
