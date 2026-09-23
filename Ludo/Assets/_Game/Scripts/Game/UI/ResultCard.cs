using TMPro;
using UnityEngine;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// The reward card on the online result screen: Rank Points, XP, coins, the rank and level lines, the RANK UP / LEVEL UP
    /// banners and the optional "watch an ad" button. It only DISPLAYS a MatchSummary; every number was worked out by
    /// RewardRules before it got here.
    /// </summary>
    public sealed class ResultCard : MonoBehaviour
    {
        [SerializeField] TMP_Text headerText;          // "Opponent disconnected." / "Better luck next time!"
        [SerializeField] GameObject rankRow;
        [SerializeField] TMP_Text rankValue;           // +25   (+50 after the ad)
        [SerializeField] GameObject xpRow;
        [SerializeField] TMP_Text xpValue;
        [SerializeField] GameObject coinsRow;
        [SerializeField] TMP_Text coinsValue;
        [SerializeField] GameObject tierRow;
        [SerializeField] TMP_Text tierValue;           // Gold  980 > 1005
        [SerializeField] GameObject levelRow;
        [SerializeField] TMP_Text levelValue;          // Level 12  (720 / 1000 XP)
        [SerializeField] TMP_Text bannerText;          // RANK UP! / LEVEL UP!
        [SerializeField] GameObject adButton;
        [SerializeField] TMP_Text adLabel;
        [SerializeField] TMP_Text adNote;              // "Rewarded ad currently unavailable."

        static readonly Color Gain = new Color(0.45f, 0.95f, 0.5f);
        static readonly Color Loss = new Color(1f, 0.5f, 0.5f);
        static readonly Color Neutral = Color.white;

        static string Signed(int v) => (v > 0 ? "+" : "") + v;
        static Color ColorOf(int v) => v > 0 ? Gain : v < 0 ? Loss : Neutral;

        /// <summary>Show the numbers of one finished match.</summary>
        public void Show(MatchSummary s, string header)
        {
            headerText.text = header ?? "";
            headerText.gameObject.SetActive(!string.IsNullOrEmpty(header));

            bool ranked = s.Mode != MatchMode.Casual;
            rankRow.SetActive(ranked);
            if (ranked)
            {
                rankValue.text = Signed(s.RankChange) + (s.AdBonusClaimed ? "   x2" : "");
                rankValue.color = ColorOf(s.RankChange);
            }
            xpRow.SetActive(true);
            xpValue.text = Signed(s.XpChange);
            xpValue.color = ColorOf(s.XpChange);
            coinsRow.SetActive(s.Coins != 0);                  // (a coin table loss shows the entry that was lost)
            coinsValue.text = Signed(s.Coins);
            coinsValue.color = Gain;

            tierRow.SetActive(ranked);
            if (ranked) tierValue.text = s.TierAfter + "   " + s.RankBefore + " > " + s.RankAfter;
            levelRow.SetActive(true);
            long into = Progression.XpIntoLevel(s.XpAfter);
            levelValue.text = s.LevelAfter >= Progression.MaxLevel ? "Level " + s.LevelAfter + "  MAX"
                : "Level " + (s.LevelUp ? s.LevelBefore + " > " + s.LevelAfter : s.LevelAfter.ToString()) + "   " + into + " / " + Progression.XpToNext(s.LevelAfter);

            // two separate events: a rank up is not a level up
            string banner = "";
            if (s.RankUp) banner += "RANK UP!  " + s.TierAfter.ToUpperInvariant();
            if (s.LevelUp) banner += (banner.Length > 0 ? "\n" : "") + "LEVEL UP!  " + s.LevelAfter;
            bannerText.text = banner;
            bannerText.gameObject.SetActive(banner.Length > 0);

            RefreshAd(s, adReady: false);
        }

        /// <summary>The ad button (only for a win that can still be doubled) or the "unavailable" note. Called again as the ad loads.</summary>
        public void RefreshAd(MatchSummary s, bool adReady)
        {
            bool offer = s != null && s.CanOfferAd;
            adButton.SetActive(offer && adReady);
            adNote.gameObject.SetActive(offer && !adReady);
            if (offer)
            {
                int gained = s.RankChange;
                adLabel.text = "WATCH AD - DOUBLE RANK POINTS\n" + Signed(gained) + " > " + Signed(gained + s.AdBonusRank);
                adNote.text = "Rewarded ad currently unavailable.";
            }
        }

        public void SetNote(string text)
        {
            adNote.text = text;
            adNote.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
