using UnityEngine;
using Ludo.Services;

namespace Ludo.Game
{
    /// <summary>
    /// Connects the menu to the ad banner: the banner is visible only while the MAIN menu screen is showing - never on
    /// the splash, the other menu screens, in a pop-up flow or in a game. It only asks; AdsService decides whether a
    /// banner exists (ads on, SDK started, ad loaded).
    /// </summary>
    public sealed class MenuBanner : MonoBehaviour
    {
        [SerializeField] ScreenRouter router;
        [SerializeField] int mainScreen = 1;

        void OnEnable()
        {
            router.ScreenShown += OnScreenShown;
            OnScreenShown(router.Current);
        }

        void OnDisable()
        {
            router.ScreenShown -= OnScreenShown;
            AdsService.SetMenuBannerVisible(false);      // leaving the menu (e.g. into a game): the banner goes away
        }

        void OnScreenShown(int screen) => AdsService.SetMenuBannerVisible(screen == mainScreen);
    }
}
