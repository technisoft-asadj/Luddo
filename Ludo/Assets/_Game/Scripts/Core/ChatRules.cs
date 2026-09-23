using System;
using System.Text;

namespace Ludo.Core
{
    /// <summary>
    /// The rules of text chat, pure and testable: how long a message may be, what counts as empty, how fast one person may
    /// talk and a small filter for rude words. The HOST applies these to every message before it is passed on, so a
    /// modified phone cannot flood the room or send an oversized message.
    /// </summary>
    public static class ChatRules
    {
        public const int MaxLength = 120;
        public const double MinSecondsBetweenMessages = 1.5;
        public const int MaxNameLength = 20;

        static readonly string[] Rude =
        {
            "fuck", "shit", "bitch", "asshole", "bastard", "cunt", "dick", "pussy", "slut", "whore", "nigger", "nigga", "faggot", "rape"
        };

        /// <summary>Tidy a message: no control characters or line breaks, single spaces, at most MaxLength. Null when nothing is left.</summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var sb = new StringBuilder(Math.Min(text.Length, MaxLength));
            bool lastSpace = true;                       // also trims the start
            foreach (char c in text)
            {
                if (char.IsControl(c) || char.IsWhiteSpace(c))
                {
                    if (!lastSpace) sb.Append(' ');
                    lastSpace = true;
                    continue;
                }
                if (c == '|' || c == '<' || c == '>') continue;   // '|' separates our network fields; < > would be read as text formatting tags
                if (char.IsHighSurrogate(c) && sb.Length >= MaxLength - 1) break;   // never cut an emoji in half
                sb.Append(c);
                lastSpace = false;
                if (sb.Length >= MaxLength) break;
            }
            string result = sb.ToString().TrimEnd();
            return result.Length == 0 ? null : result;
        }

        /// <summary>A name that is safe to put in a network message (no separator, not too long).</summary>
        public static string CleanName(string name)
        {
            string n = Clean(name) ?? "Player";
            return n.Length > MaxNameLength ? n.Substring(0, MaxNameLength) : n;
        }

        /// <summary>Replace rude words with stars (keeps the length so the message still reads naturally).</summary>
        public static string MaskRude(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string lower = text.ToLowerInvariant();
            var chars = text.ToCharArray();
            foreach (var word in Rude)
            {
                int at = 0;
                while ((at = lower.IndexOf(word, at, StringComparison.Ordinal)) >= 0)
                {
                    for (int i = at; i < at + word.Length; i++) chars[i] = '*';
                    at += word.Length;
                }
            }
            return new string(chars);
        }

        /// <summary>Everything the host does to an incoming message: clean, then mask. Null = drop it.</summary>
        public static string Prepare(string text)
        {
            string clean = Clean(text);
            return clean == null ? null : MaskRude(clean);
        }
    }

    /// <summary>Lets one sender through at most once every ChatRules.MinSecondsBetweenMessages.</summary>
    public sealed class ChatThrottle
    {
        double nextAllowed = double.MinValue;

        public bool Allow(double nowSeconds)
        {
            if (nowSeconds < nextAllowed) return false;
            nextAllowed = nowSeconds + ChatRules.MinSecondsBetweenMessages;
            return true;
        }
    }
}
