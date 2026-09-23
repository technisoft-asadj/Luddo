using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ludo.Game
{
    /// <summary>The one place that changes scenes, so every screen leaves a scene the same way.</summary>
    public static class SceneLoader
    {
        public const string Menu = "Menu";
        public const string Game = "Game";

        public static void Load(string sceneName)
        {
            Time.timeScale = 1f;   // a paused game must not freeze the next scene
            SceneManager.LoadScene(sceneName);
        }
    }
}
