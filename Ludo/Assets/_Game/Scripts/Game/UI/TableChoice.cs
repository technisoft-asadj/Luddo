using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// What Select Mode has already decided, so later screens do not ask again: how many players sit at the table, and
    /// whether the rules were picked there too. A row named "1 vs 1" must never offer 3 or 4 players, and Team Up is
    /// always four. Cleared whenever the main menu is shown.
    /// </summary>
    public static class TableChoice
    {
        /// <summary>The player count chosen in Select Mode / the play-how screen, 0 = not chosen yet.</summary>
        public static int Players;
        /// <summary>True when the row itself named the size (1 vs 1, 4 Player): the play-how screen must not offer others.</summary>
        public static bool SizeFixed;
        /// <summary>True when the rules were chosen in Select Mode: the other screens then show them instead of a picker.</summary>
        public static bool ModeLocked;

        /// <summary>The seat count that is already decided for this mode, or 0 when the player may still choose.</summary>
        public static int Seats(GameMode mode) => GameSession.NeedsFourPlayers(mode) ? 4 : Players;

        public static void Reset() { Players = 0; SizeFixed = false; ModeLocked = false; }
    }

    /// <summary>
    /// A row of choice chips that can collapse to just the chosen one, centred and wider. Used where the choice was
    /// already made in Select Mode: the screen shows what was picked instead of options that do not apply.
    /// </summary>
    public sealed class ChipRow
    {
        readonly Image[] chips;
        readonly Vector2[] home;
        readonly Vector2[] size;

        public ChipRow(Image[] chips)
        {
            this.chips = chips;
            home = new Vector2[chips.Length];
            size = new Vector2[chips.Length];
            for (int i = 0; i < chips.Length; i++)
            {
                home[i] = chips[i].rectTransform.anchoredPosition;
                size[i] = chips[i].rectTransform.sizeDelta;
            }
        }

        /// <summary>Show every chip (only = -1) or just the chip with this index, centred.</summary>
        public void Show(int only)
        {
            for (int i = 0; i < chips.Length; i++)
            {
                var rt = chips[i].rectTransform;
                bool on = only < 0 || i == only;
                chips[i].gameObject.SetActive(on);
                if (only < 0) { rt.anchoredPosition = home[i]; rt.sizeDelta = size[i]; }
                else if (on) { rt.anchoredPosition = new Vector2(0f, home[i].y); rt.sizeDelta = new Vector2(size[i].x * 1.5f, size[i].y); }
            }
        }
    }
}
