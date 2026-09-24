using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// The bar across the top of the main menu: the player's picture, name, country flag, rank and coins.
    ///
    /// It lives in the online assembly because only that side can see the cloud profile (StatsService), but it must work
    /// perfectly well before anyone signs in: the name and picture then come from the phone's own saved profile and the
    /// rank and coins simply stay hidden rather than showing invented numbers.
    /// </summary>
    public sealed class MenuTopBar : MonoBehaviour
    {
        [SerializeField] Image avatar;
        [SerializeField] Image flag;
        [SerializeField] TMP_Text nameText;
        [SerializeField] GameObject rankPill;
        [SerializeField] TMP_Text rankText;
        [SerializeField] GameObject coinPill;
        [SerializeField] TMP_Text coinText;

        void OnEnable()
        {
            GameSettings.Changed += Refresh;
            StatsService.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            GameSettings.Changed -= Refresh;
            StatsService.Changed -= Refresh;
        }

        void Refresh()
        {
            if (avatar != null) avatar.sprite = GameSettings.Picture(0);
            if (nameText != null) nameText.text = GameSettings.PlayerName(0);
            if (flag != null) FlagLibrary.Apply(flag, GameSettings.Country);

            bool signedIn = StatsService.Loaded;                 // no cloud profile yet: show nothing rather than zeros
            if (rankPill != null) rankPill.SetActive(signedIn);
            if (coinPill != null) coinPill.SetActive(signedIn);
            if (!signedIn) return;
            var s = StatsService.Mine;
            if (rankText != null) rankText.text = s.Tier;
            if (coinText != null) coinText.text = s.coins.ToString("N0");
        }
    }
}
