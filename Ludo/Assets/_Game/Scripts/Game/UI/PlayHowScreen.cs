using TMPro;
using UnityEngine;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// The screen every Select Mode row leads to: "how do you want to play this?". It shows the chosen mode (or the
    /// 1 vs 1 / 4 Player table size) with its rules, and the ways to play it: online, with friends in a private room,
    /// against the computer, or on one phone. The choice itself lives in ModePicker and the table-size preference; this
    /// screen only names what was chosen.
    /// </summary>
    public sealed class PlayHowScreen : MonoBehaviour
    {
        /// <summary>Set by the Select Mode row: a heading that replaces the mode name ("1 vs 1", "4 Player"), or empty.</summary>
        public static string Heading = "";
        /// <summary>The table size the row asked for (2 or 4), 0 = whatever the mode needs.</summary>
        public static int Size;

        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text blurb;

        void OnEnable() => Refresh();

        void Refresh()
        {
            var mode = ModePicker.Current;
            title.text = string.IsNullOrEmpty(Heading) ? GameSession.ModeName(mode) : Heading;
            string players = Size == 2 ? "2 players" : Size == 4 || GameSession.NeedsFourPlayers(mode) ? "4 players" : "2 - 4 players";
            blurb.text = GameSession.ModeRule(mode) + "  ·  " + players;
        }
    }
}
