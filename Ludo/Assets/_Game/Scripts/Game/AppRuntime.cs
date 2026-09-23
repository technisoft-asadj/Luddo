using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// App-wide settings that must be right before the first screen: a steady 60 frames per second (Unity's Android
    /// default is 30, which makes pawn moves and menus look choppy) and a portrait-only screen.
    /// </summary>
    public static class AppRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
            QualitySettings.vSyncCount = 0;          // vsync off: targetFrameRate decides
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
        }
    }
}
