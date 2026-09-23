using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Game
{
    /// <summary>Fills the loading bar, then opens the main menu. Only shown once per app start
    /// (coming back from a game goes straight to the menu).</summary>
    public sealed class SplashScreen : MonoBehaviour
    {
        static bool alreadyShown;

        // In the Editor, static values can survive between Play sessions; a real app start always begins fresh.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => alreadyShown = false;

        [SerializeField] ScreenRouter router;
        [SerializeField] Image bar;                    // an Image of type "Filled"
        [SerializeField] int mainMenuScreen = 1;
        [SerializeField] int roomScreen = 9;
        [SerializeField] int loginScreen = 14;          // the welcome page: Google / Facebook / Guest
        [SerializeField] float seconds = 1.6f;

        IEnumerator Start()
        {
            if (alreadyShown)
            {
                router.Replace(mainMenuScreen);
                if (GameSession.ReturnToRoom)                      // an online rematch: back to the waiting room, same people
                {
                    GameSession.ReturnToRoom = false;
                    router.Show(roomScreen);
                }
                yield break;
            }
            alreadyShown = true;
            float start = Time.realtimeSinceStartup;              // real time, so a slow first frame cannot cut the splash short
            for (float t = 0f; t < seconds; t = Time.realtimeSinceStartup - start)
            {
                bar.fillAmount = t / seconds;
                yield return null;
            }
            bar.fillAmount = 1f;
            if (LoginGate.HasChosen()) router.Replace(mainMenuScreen);
            else router.Replace(loginScreen);                        // not signed in: the welcome page (Google, Facebook or Guest)
        }
    }
}
