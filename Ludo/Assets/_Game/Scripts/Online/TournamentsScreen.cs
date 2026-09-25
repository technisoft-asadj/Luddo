using System;
using TMPro;
using UnityEngine;
using Ludo.Core;

namespace Ludo.Online
{
    /// <summary>
    /// Select Mode's "Tournaments". The one tournament this game runs is the Weekly Cup: every ranked match earns points
    /// (a win more than a played match), the board resets each Monday. This screen shows the live state of it - time
    /// left, my points this week, how points are earned - and the buttons to play a ranked match or open the board.
    /// It shows only what the cup really does; there are no prizes, brackets or entry fees to list.
    /// </summary>
    public sealed class TournamentsScreen : MonoBehaviour
    {
        [SerializeField] TMP_Text timeLeft;
        [SerializeField] TMP_Text myPoints;
        [SerializeField] TMP_Text howItWorks;

        float next;

        void OnEnable()
        {
            StatsService.Changed += Refresh;
            howItWorks.text = "Win a ranked match:  +" + WeeklyCup.WinPoints + " points\nPlay a ranked match:  +" + WeeklyCup.PlayPoints + " points\nThe board starts again every Monday.";
            Refresh();
        }

        void OnDisable() => StatsService.Changed -= Refresh;

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 2f;
            Refresh();
        }

        void Refresh()
        {
            var left = WeeklyCup.TimeLeft(DateTime.UtcNow);
            timeLeft.text = left.Days > 0 ? left.Days + "d " + left.Hours + "h" : left.Hours + "h " + left.Minutes + "m";
            myPoints.text = StatsService.Loaded ? StatsService.Mine.weeklyPoints.ToString("N0") : "-";
        }
    }
}
