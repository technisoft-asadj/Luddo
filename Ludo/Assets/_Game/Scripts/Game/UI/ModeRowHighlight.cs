using UnityEngine;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// The Select Mode screen's rule rows (Classic, Master, Quick/Blitz, Team Up). Tapping one chooses the rules for the
    /// next game; this only shows which one is chosen, by lighting a ring around that row.
    ///
    /// It deliberately does not recolour the rows the way the chip picker does: each row has its own colour from the
    /// reference art, and repainting them would throw that away.
    /// </summary>
    public sealed class ModeRowHighlight : MonoBehaviour
    {
        [SerializeField] GameObject[] rings;        // one per row, in the order of 'modes'
        [SerializeField] int[] modes;               // the GameMode each row stands for

        void OnEnable()
        {
            ModePicker.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => ModePicker.Changed -= Refresh;

        /// <summary>A rule row was tapped (wired with the GameMode number).</summary>
        public void Choose(int mode) => ModePicker.Current = (GameMode)mode;

        void Refresh()
        {
            int current = (int)ModePicker.Current;
            for (int i = 0; i < rings.Length && i < modes.Length; i++)
                if (rings[i] != null) rings[i].SetActive(modes[i] == current);
        }
    }
}
