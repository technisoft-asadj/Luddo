using UnityEngine;
using Ludo.Services;

namespace Ludo.Game
{
    /// <summary>
    /// Connects the menu to the ad banner: on the menu the banner is visible only while the MAIN screen is showing (never on
    /// the splash, the other menu screens or in a pop-up flow); games turn it on themselves (GameController). It only asks; AdsService decides whether a
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

        // After every OnEnable/OnDestroy of a scene change has run (a game may be closing as the menu opens), say again what is wanted.
        void Start() => OnScreenShown(router.Current);

        void OnDisable()
        {
            router.ScreenShown -= OnScreenShown;
            AdsService.SetMenuBannerVisible(false);      // leaving the menu (e.g. into a game): the banner goes away
        }

        void OnScreenShown(int screen) => AdsService.SetMenuBannerVisible(screen == mainScreen);
    }
}
