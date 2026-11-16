using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.AI;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// Team Up, offline: "who is my partner?" Three chips, shown only while the Team Up rules are chosen. Against the computer the
    /// chips are the partner's strength (Easy / Medium / Hard); on one shared phone they are the other three people. The partner
    /// always sits across the table (see GameSession.ConfigureLocal / ConfigureVsAi).
    /// </summary>
    public sealed class PartnerPicker : MonoBehaviour
    {
        public enum Kind { Computer, Friend }

        [SerializeField] Kind kind;
        [SerializeField] GameObject row;                 // label + chips: switched on only for Team Up
        [SerializeField] GameObject[] hideInTeamUp;      // things this row replaces (the "Opponents" chips: Team Up is always 3)
        [SerializeField] Image[] chips;
        [SerializeField] TMP_Text[] labels;
        [SerializeField] Color selectedColor = new Color(0.86f, 1f, 0.88f);
        [SerializeField] Color normalColor = new Color(0.93f, 0.96f, 1f);

        /// <summary>A partner was picked: the player rows on Select Players show the new seat colours.</summary>
        public static event System.Action Chosen;

        static readonly string[] Levels = { "Easy", "Medium", "Hard" };

        void OnEnable()
        {
            ModePicker.Changed += Refresh;
            GameSettings.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            ModePicker.Changed -= Refresh;
            GameSettings.Changed -= Refresh;
        }

        /// <summary>Chip tapped (wired with its number 0..2).</summary>
        public void Choose(int index)
        {
            if (kind == Kind.Computer) GameSession.PartnerLevel = (AiDifficulty)Mathf.Clamp(index, 0, 2);
            else GameSession.LocalPartner = Mathf.Clamp(index + 1, 1, 3);
            Refresh();
            Chosen?.Invoke();
        }

        void Refresh()
        {
            bool team = ModePicker.Current == GameMode.TeamUp;
            if (row != null) row.SetActive(team);
            if (hideInTeamUp != null)
                foreach (var go in hideInTeamUp) if (go != null) go.SetActive(!team);
            if (!team) return;

            int selected = kind == Kind.Computer ? (int)GameSession.PartnerLevel : (GameSession.LocalPartner == 0 ? 2 : GameSession.LocalPartner) - 1;
            for (int i = 0; i < chips.Length; i++)
            {
                if (labels[i] != null) labels[i].text = kind == Kind.Computer ? Levels[i] : GameSettings.PlayerName(i + 1);
                chips[i].color = i == selected ? selectedColor : normalColor;
            }
        }
    }
}
