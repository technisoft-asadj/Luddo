using System;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>Why the login page (Google / Facebook / Guest) is open, which decides where it goes when the player is done.</summary>
    public enum LoginPurpose
    {
        Start,      // first launch, or after logging out / deleting the account: done -> main menu (no way back)
        Online      // Play Online was tapped while nobody is logged in: done -> the online menu (back -> where they came from)
    }

    /// <summary>
    /// Lets the splash screen (game code) ask "has this player already chosen how to log in?" without knowing anything about
    /// the online services, and tells the login page why it was opened. The online code fills in HasChosen when it starts.
    /// </summary>
    public static class LoginGate
    {
        /// <summary>True when the player already chose Guest or an account (or when there is no online code at all).</summary>
        public static Func<bool> HasChosen = () => true;

        public static LoginPurpose Purpose = LoginPurpose.Start;

        /// <summary>A one-off line for the login page to show when it opens (e.g. "Your online account was deleted.").</summary>
        public static string Notice = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Purpose = LoginPurpose.Start; Notice = ""; }
    }
}
