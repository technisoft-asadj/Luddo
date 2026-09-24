namespace Ludo.Core
{
    /// <summary>
    /// The one-tap phrases next to the emoji buttons, so a player can react during a match without typing. They are sent as
    /// ordinary chat messages, which means they pass the same filter, the same length limit and the same throttle as anything
    /// typed by hand (ChatRules) - a quick button is never a way round the spam rules.
    /// </summary>
    public static class QuickChat
    {
        public static readonly string[] Phrases =
        {
            "Good luck!",
            "Nice move!",
            "Wow!",
            "Oops!",
            "Your turn",
            "Well played!",
            "Hurry up!",
            "Good game!"
        };

        public static bool IsValidIndex(int index) => index >= 0 && index < Phrases.Length;

        /// <summary>The phrase for a button, or "" if the button number makes no sense.</summary>
        public static string Get(int index) => IsValidIndex(index) ? Phrases[index] : "";
    }
}
