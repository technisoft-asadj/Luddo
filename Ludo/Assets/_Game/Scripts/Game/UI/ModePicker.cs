using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// The row of game-mode chips (Classic / Master / Arrow / Blitz) with one line explaining the chosen mode. Every picker in
    /// the menus shows and changes the same saved choice, which becomes GameSession.Mode for the next offline game and the mode
    /// of the next room / Quick Match search.
    /// </summary>
    public sealed class ModePicker : MonoBehaviour
    {
        const string Key = "ludo.mode";

        [SerializeField] Image[] chips;                 // index = (int)GameMode
        [SerializeField] TMP_Text[] labels;
        [SerializeField] TMP_Text ruleText;
        [SerializeField] Color selectedColor = new Color(1f, 0.82f, 0.15f);
        [SerializeField] Color normalColor = new Color(0.94f, 0.97f, 1f);
        [SerializeField] Color selectedText = new Color(0.08f, 0.18f, 0.45f);
        [SerializeField] Color normalText = new Color(0.08f, 0.18f, 0.45f);

        static event System.Action Changed;

        /// <summary>The saved mode choice.</summary>
        public static GameMode Current
        {
            get
            {
                int v = PlayerPrefs.GetInt(Key, 0);
                return v >= 0 && v <= (int)GameMode.Blitz ? (GameMode)v : GameMode.Classic;
            }
            set
            {
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        void OnEnable()
        {
            Changed += Refresh;
            Refresh();
        }

        void OnDisable() => Changed -= Refresh;

        /// <summary>A chip was tapped (wired with the mode number).</summary>
        public void Choose(int mode)
        {
            if (mode < 0 || mode > (int)GameMode.Blitz) return;
            Current = (GameMode)mode;
        }

        void Refresh()
        {
            var mode = Current;
            for (int i = 0; i < chips.Length; i++)
            {
                bool on = i == (int)mode;
                chips[i].color = on ? selectedColor : normalColor;
                if (labels != null && i < labels.Length && labels[i] != null) labels[i].color = on ? selectedText : normalText;
            }
            if (ruleText != null) ruleText.text = GameSession.ModeRule(mode);
        }
    }
}
