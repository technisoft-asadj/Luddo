using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// The screen every Select Mode row leads to: "how do you want to play this?". It shows the chosen mode (or the
    /// 1 vs 1 / 4 Player table size / Offline) with its rules, asks how many players when the row has not already
    /// decided that, and lists the ways to play: online, with friends in a private room, against the computer, or on
    /// one phone. What was chosen is kept in ModePicker and TableChoice; later screens read it from there.
    /// </summary>
    public sealed class PlayHowScreen : MonoBehaviour
    {
        /// <summary>Set by the Select Mode row: a heading that replaces the mode name ("1 vs 1", "4 Player", "Offline"), or empty.</summary>
        public static string Heading = "";
        /// <summary>The Offline row: only the two ways that need no internet.</summary>
        public static bool OfflineOnly;

        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text blurb;
        [SerializeField] TMP_Text playersLabel;
        [SerializeField] Image[] chips;                 // 2, 3, 4 players
        [SerializeField] TMP_Text[] chipLabels;
        [SerializeField] RectTransform[] rows;          // online, friends, ai, pass & play
        [SerializeField] VerticalSpread spread;
        [SerializeField] Color chipOn = new Color(1f, 0.82f, 0.15f);
        [SerializeField] Color chipOff = new Color(0.93f, 0.96f, 1f);

        ChipRow chipRow;
        Vector2[] homes;
        float[] weights;

        void OnEnable()
        {
            var mode = ModePicker.Current;
            if (TableChoice.Seats(mode) == 0) TableChoice.Players = 2;      // a free choice starts on 2 players
            Refresh();
        }

        /// <summary>A players chip was tapped (wired with 2, 3 or 4).</summary>
        public void SetPlayers(int count)
        {
            if (TableChoice.Seats(ModePicker.Current) > 0 && (TableChoice.SizeFixed || GameSession.NeedsFourPlayers(ModePicker.Current))) return;
            TableChoice.Players = Mathf.Clamp(count, 2, 4);
            Refresh();
        }

        void Refresh()
        {
            var mode = ModePicker.Current;
            title.text = string.IsNullOrEmpty(Heading) ? GameSession.ModeName(mode) : Heading;
            blurb.text = OfflineOnly ? "Play without internet  ·  " + GameSession.ModeName(mode) : GameSession.ModeRule(mode);

            int seats = TableChoice.Seats(mode);
            bool free = !TableChoice.SizeFixed && !GameSession.NeedsFourPlayers(mode);
            if (chipRow == null) chipRow = new ChipRow(chips);
            chipRow.Show(free ? -1 : seats - 2);                  // a fixed table shows just its own size
            for (int i = 0; i < chips.Length; i++)
            {
                bool on = i + 2 == seats;
                chips[i].color = on ? chipOn : chipOff;
                if (i < chipLabels.Length) chipLabels[i].color = new Color(0.08f, 0.18f, 0.45f);
            }
            playersLabel.text = free ? "How many players?" : "Players";

            // Offline has no online rows: the other two move up into their place
            if (homes == null)
            {
                homes = new Vector2[rows.Length]; weights = new float[rows.Length];
                for (int i = 0; i < rows.Length; i++) { homes[i] = spread.Home(i); weights[i] = spread.Weight(i); }
            }
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].gameObject.SetActive(!(OfflineOnly && i < 2));
                spread.SetWeight(i, weights[OfflineOnly && i >= 2 ? i - 2 : i]);
                spread.SetHome(i, homes[OfflineOnly && i >= 2 ? i - 2 : i]);
            }
        }
    }
}
