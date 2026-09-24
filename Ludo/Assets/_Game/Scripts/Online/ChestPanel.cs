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
    /// The free chest pop-up: a pile of coins that fills up again every few hours, so a player who has spent everything
    /// always has a way back without paying. Opening it is free; the rewarded ad next to it only ever doubles what was
    /// already given, and only after the ads SDK confirms the ad actually finished.
    /// </summary>
    public sealed class ChestPanel : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] RectTransform chestArt;         // bounces when the chest is ready
        [SerializeField] TMP_Text timerText;             // "Ready!" / "2h 14m"
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button openButton;
        [SerializeField] TMP_Text openLabel;
        [SerializeField] Button adButton;

        bool busy;
        float bounce;

        /// <summary>Is a free chest waiting? (the red dot on the chest button)</summary>
        public static bool Claimable => StatsService.ChestReady;

        public void Open()
        {
            panel.SetActive(true);
            statusText.text = "";
            busy = false;
            Refresh();
        }

        public void Close() => panel.SetActive(false);

        void OnEnable() => StatsService.Changed += Refresh;

        void OnDisable() => StatsService.Changed -= Refresh;

        void Refresh()
        {
            bool ready = StatsService.ChestReady;
            timerText.text = StatsService.Loaded ? Chest.Countdown(StatsService.Mine.chestAt, DateTime.UtcNow) : "...";
            openButton.gameObject.SetActive(ready);
            openButton.interactable = ready && !busy;
            openLabel.text = "Open";
            adButton.gameObject.SetActive(ready);
            adButton.interactable = ready && !busy && AdsService.RewardedReady;
            if (!StatsService.Loaded) statusText.text = "Connecting...";
            else if (!ready && statusText.text.Length == 0)
                statusText.text = "Your next free chest is on its way. Play a few games while you wait!";
        }

        void Update()
        {
            if (!panel.activeSelf) return;
            Refresh();                                                       // the countdown ticks while the pop-up is open
            if (chestArt == null) return;
            bool ready = StatsService.ChestReady;
            bounce = ready ? bounce + Time.unscaledDeltaTime * 3.2f : 0f;     // a locked chest sits still
            float k = ready ? 1f + Mathf.Abs(Mathf.Sin(bounce)) * 0.06f : 1f;
            chestArt.localScale = new Vector3(k, k, 1f);
        }

        /// <summary>Take the coins.</summary>
        public void Claim() => Take(false);

        /// <summary>Watch a rewarded ad for double coins. Nothing extra is given unless the SDK says the ad finished.</summary>
        public void WatchAd()
        {
            if (busy) return;
            busy = true;
            statusText.text = "Loading the ad...";
            AdsService.ShowRewarded(rewarded =>
            {
                busy = false;
                if (this == null) return;
                if (!rewarded) { statusText.text = "The ad did not finish - the chest is still yours to open."; Refresh(); return; }
                Take(true);
            });
        }

        async void Take(bool doubled)
        {
            if (busy && !doubled) return;
            busy = true;
            openButton.interactable = false;
            adButton.interactable = false;
            int got = await StatsService.OpenChestAsync(doubled);
            busy = false;
            if (this == null) return;
            statusText.text = got > 0 ? "+" + got.ToString("N0") + " coins!" : "That chest is not ready yet.";
            if (got > 0) AudioService.Play(SfxId.Win);
            Refresh();
        }
    }
}
