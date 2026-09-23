using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;
using Ludo.Game;
using Ludo.Services;

namespace Ludo.Online
{
    /// <summary>
    /// The daily reward pop-up (like Ludo Star): a 7-day calendar of coin gifts. "Claim" takes today's coins; "Watch ad x2"
    /// shows a rewarded ad and takes double - ONLY after the ads SDK confirms the reward (a skipped or failed ad gives nothing
    /// extra, and the normal claim is still there). Coming back every day moves along the calendar; a missed day starts again.
    /// </summary>
    public sealed class DailyRewardPanel : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Image[] dayTiles;              // 7 tiles
        [SerializeField] TMP_Text[] dayAmounts;
        [SerializeField] GameObject[] dayTicks;         // shown on the days already claimed in this run
        [SerializeField] Button claimButton;
        [SerializeField] Button adButton;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Color todayColor = new Color(1f, 0.82f, 0.15f);
        [SerializeField] Color doneColor = new Color(0.72f, 0.92f, 0.74f);
        [SerializeField] Color laterColor = new Color(0.93f, 0.96f, 1f);

        const string ShownKey = "ludo.daily.shown";     // the day the pop-up opened by itself last (once a day)
        bool busy;

        /// <summary>Is there a reward to claim today? (the gift button's red dot)</summary>
        public static bool Claimable => StatsService.Loaded && DailyReward.CanClaim(StatsService.Mine.dailyDay, DateTime.Now);

        /// <summary>Open by itself once a day, the first time Play Online opens with a reward waiting.</summary>
        public void OpenIfNew()
        {
            int today = DailyReward.DayKey(DateTime.Now);
            if (!Claimable || PlayerPrefs.GetInt(ShownKey, 0) == today) return;
            PlayerPrefs.SetInt(ShownKey, today);
            PlayerPrefs.Save();
            Open();
        }

        public void Open()
        {
            panel.SetActive(true);
            statusText.text = "";
            busy = false;
            Refresh();
        }

        public void Close() => panel.SetActive(false);

        void Refresh()
        {
            var s = StatsService.Mine;
            var today = DateTime.Now;
            bool can = StatsService.Loaded && DailyReward.CanClaim(s.dailyDay, today);
            int day = DailyReward.NextDay(s.dailyDay, s.dailyStreak, today);        // today's calendar day (1..7)
            for (int i = 0; i < dayTiles.Length; i++)
            {
                int d = i + 1;
                dayAmounts[i].text = DailyReward.AmountFor(d).ToString("N0");
                bool claimed = can ? d < day : d <= day;
                bool isToday = can && d == day;
                dayTiles[i].color = isToday ? todayColor : claimed ? doneColor : laterColor;
                dayTicks[i].SetActive(claimed);
            }
            claimButton.interactable = can && !busy;
            claimButton.gameObject.SetActive(can);
            adButton.gameObject.SetActive(can);
            adButton.interactable = can && !busy && AdsService.RewardedReady;
            if (!StatsService.Loaded) statusText.text = "Connecting...";
            else if (!can) statusText.text = "Come back tomorrow for day " + DailyReward.NextDay(s.dailyDay, s.dailyStreak, today.AddDays(1)) + "!";
        }

        void Update()
        {
            if (panel.activeSelf && !busy) adButton.interactable = adButton.gameObject.activeSelf && AdsService.RewardedReady;
        }

        /// <summary>Claim today's coins.</summary>
        public void Claim() => Take(false);

        /// <summary>Watch a rewarded ad for double coins.</summary>
        public void WatchAd()
        {
            if (busy) return;
            busy = true;
            statusText.text = "Loading the ad...";
            AdsService.ShowRewarded(rewarded =>
            {
                busy = false;
                if (this == null) return;
                if (!rewarded) { statusText.text = "The ad did not finish - you can still claim normally."; Refresh(); return; }
                Take(true);
            });
        }

        async void Take(bool doubled)
        {
            if (busy && !doubled) return;
            busy = true;
            claimButton.interactable = false;
            adButton.interactable = false;
            int got = await StatsService.ClaimDailyAsync(doubled);
            busy = false;
            if (this == null) return;
            Refresh();
            statusText.text = got > 0 ? "+" + got.ToString("N0") + " coins!" : "Already claimed today.";
            if (got > 0) AudioService.Play(SfxId.Win);
        }
    }
}
