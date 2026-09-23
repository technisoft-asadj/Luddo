using NUnit.Framework;
using Ludo.Core;

namespace Ludo.Tests
{
    public class ChatEmojiTests
    {
        [Test]
        public void KnownEmojiBecomesASpriteTag()
        {
            Assert.AreEqual("gg <sprite name=\"1f602\">", ChatEmoji.ToRichText("gg \U0001F602"));
            Assert.AreEqual("<sprite name=\"2764\">", ChatEmoji.ToRichText("❤️"));
        }

        [Test]
        public void UnknownEmojiIsDroppedInsteadOfShowingABox()
        {
            Assert.AreEqual("hi ", ChatEmoji.ToRichText("hi \U0001F99C"));        // parrot: not in the set
            Assert.AreEqual("ok", ChatEmoji.ToRichText("ok\uD83D"));              // a broken half of an emoji
        }

        [Test]
        public void PlainTextIsUnchanged()
        {
            Assert.AreEqual("Nice move, well played!", ChatEmoji.ToRichText("Nice move, well played!"));
        }

        [Test]
        public void EveryEmojiButtonSurvivesTheChatRules()
        {
            foreach (int code in ChatEmoji.Codes)
                Assert.AreEqual(ChatEmoji.Text(code), ChatRules.Prepare(ChatEmoji.Text(code)));
        }

        [Test]
        public void CleanNeverCutsAnEmojiInHalf()
        {
            string text = new string('a', ChatRules.MaxLength - 1) + "\U0001F602";
            string clean = ChatRules.Clean(text);
            Assert.IsFalse(char.IsHighSurrogate(clean[clean.Length - 1]));
        }
    }
}
