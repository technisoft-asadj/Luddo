using System.Text;

namespace Ludo.Core
{
    /// <summary>
    /// The emojis the chat can show (Noto Emoji pictures, one per code point). They travel as ordinary Unicode characters, so a
    /// player can tap one in the chat window or type it from the phone keyboard. Emojis outside this set are dropped when shown.
    /// </summary>
    public static class ChatEmoji
    {
        public static readonly int[] Codes =
        {
            0x1F602, 0x1F923, 0x1F600, 0x1F60A, 0x1F609, 0x1F61C, 0x1F60E, 0x1F60D, 0x1F973, 0x1F914,
            0x1F62E, 0x1F631, 0x1F622, 0x1F62D, 0x1F621, 0x1F624, 0x1F634, 0x1F648, 0x1F480, 0x1F64F,
            0x1F44D, 0x1F44E, 0x1F44F, 0x1F4AA, 0x1F44B, 0x1F525, 0x1F451, 0x1F3B2, 0x1F3C6, 0x2764
        };

        public static string Text(int code) => char.ConvertFromUtf32(code);

        public static string Name(int code) => code.ToString("x");

        public static bool IsKnown(int code)
        {
            foreach (int c in Codes) if (c == code) return true;
            return false;
        }

        /// <summary>
        /// Chat text ready for a TextMeshPro label: known emojis become sprite tags, variation selectors and joiners are removed,
        /// and any other emoji (which the game has no picture for) is dropped instead of showing an empty box.
        /// </summary>
        public static string ToRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text.Length + 16);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '️' || c == '︎' || c == '‍') continue;
                int code = c;
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1])) continue;
                    code = char.ConvertToUtf32(c, text[i + 1]);
                    i++;
                }
                else if (char.IsLowSurrogate(c)) continue;

                if (IsKnown(code)) sb.Append("<sprite name=\"").Append(Name(code)).Append("\">");
                else if (code > 0xFFFF || (code >= 0x2600 && code <= 0x27BF)) continue;   // an emoji we have no picture for
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
