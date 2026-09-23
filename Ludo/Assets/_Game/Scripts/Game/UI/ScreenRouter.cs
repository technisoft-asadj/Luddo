using System.Collections.Generic;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// Shows exactly one menu screen at a time and remembers where you came from, so "Back" (the arrow on screen
    /// AND the Android back button/gesture) returns to the previous screen. On the first screen, Back closes the app.
    /// Screens are plain GameObjects; buttons call Show(index) through the Inspector (Button > On Click).
    /// </summary>
    public sealed class ScreenRouter : MonoBehaviour
    {
        [SerializeField] GameObject[] screens;         // index = screen number used by the buttons
        [SerializeField] GameObject[] modals;          // pop-ups that Back closes first
        [SerializeField] int firstScreen;

        readonly Stack<int> history = new Stack<int>();

        /// <summary>Raised every time a screen becomes the visible one (with its number). The main-menu banner listens to this.</summary>
        public event System.Action<int> ScreenShown;

        /// <summary>The screen on show right now, or -1 before the first one.</summary>
        public int Current => history.Count > 0 ? history.Peek() : -1;

        /// <summary>The screen you would return to with Back, or -1.</summary>
        public int Previous
        {
            get
            {
                if (history.Count < 2) return -1;
                var all = history.ToArray();      // the top of the stack comes first
                return all[1];
            }
        }

        void Awake()
        {
            Push(firstScreen);
        }

        void Update()
        {
            // Android's back button/gesture arrives as the Escape key in Unity
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            foreach (var m in modals)
                if (m != null && m.activeSelf) { m.SetActive(false); return; }
            if (history.Count > 1) Back();
            else Application.Quit();
        }

        public void Show(int index) => Push(index);

        /// <summary>Go to a screen WITHOUT leaving the current one in the history (used after the splash).</summary>
        public void Replace(int index)
        {
            if (history.Count > 0) history.Pop();
            Push(index);
        }

        /// <summary>Start over at this screen with an empty history (after logging out: Back must not return into the old session).</summary>
        public void ResetTo(int index)
        {
            history.Clear();
            Push(index);
        }

        public void Back()
        {
            if (history.Count <= 1) return;
            history.Pop();
            Apply(history.Peek());
        }

        void Push(int index)
        {
            history.Push(index);
            Apply(index);
        }

        void Apply(int index)
        {
            for (int i = 0; i < screens.Length; i++) screens[i].SetActive(i == index);
            foreach (var m in modals) if (m != null) m.SetActive(false);
            ScreenShown?.Invoke(index);
        }
    }
}
