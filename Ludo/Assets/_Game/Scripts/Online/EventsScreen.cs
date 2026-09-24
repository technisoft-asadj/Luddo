using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;

namespace Ludo.Online
{
    /// <summary>
    /// The Events screen from the reference: the things a player can collect outside a match. It lists only what this
    /// game really has - the daily reward calendar, the free chest and the Weekly Cup - each showing its live state
    /// (ready, or how long until it is). The reference also shows a Season Pass and timed special events; those have no
    /// system behind them yet, so they are not drawn here rather than shown as cards that cannot be opened.
    /// </summary>
    public sealed class EventsScreen : MonoBehaviour
    {
        [SerializeField] TMP_Text dailyState;
        [SerializeField] GameObject dailyDot;
        [SerializeField] TMP_Text chestState;
        [SerializeField] GameObject chestDot;
        [SerializeField] TMP_Text cupState;

        float next;

        void OnEnable()
        {
            StatsService.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => StatsService.Changed -= Refresh;

        /// <summary>The chest fills up on a clock, so the line is refreshed while the screen is open.</summary>
        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 2f;
            Refresh();
        }

        void Refresh()
        {
            bool loaded = StatsService.Loaded;
            bool daily = DailyRewardPanel.Claimable;
            if (dailyState != null)
                dailyState.text = !loaded ? "Connecting..." : daily ? "Ready to claim!" : "Claimed - come back tomorrow";
            if (dailyDot != null) dailyDot.SetActive(daily);

            bool chest = ChestPanel.Claimable;
            if (chestState != null)
                chestState.text = !loaded ? "Connecting..."
                    : chest ? "Ready to open!"
                    : "Next chest in " + Chest.Countdown(StatsService.Mine.chestAt, System.DateTime.UtcNow);
            if (chestDot != null) chestDot.SetActive(chest);

            if (cupState != null)
            {
                var left = WeeklyCup.TimeLeft(System.DateTime.UtcNow);
                cupState.text = !loaded ? "Connecting..."
                    : "Ends in " + (left.Days > 0 ? left.Days + "d " + left.Hours + "h" : left.Hours + "h " + left.Minutes + "m")
                      + "   ·   " + StatsService.Mine.weeklyPoints + " pts";
            }
        }
    }
}
