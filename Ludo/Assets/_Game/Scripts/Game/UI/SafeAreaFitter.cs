using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// Keeps a UI panel inside the "safe area" of the phone: away from the camera notch, the rounded corners and
    /// the system gesture bars. Android draws apps edge-to-edge when they target Android 15+ (a Google Play
    /// requirement), so every screen's buttons and text must live inside a panel with this component.
    /// Backgrounds can stay outside it and fill the whole screen.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        Vector2Int screenSize;

        void Update()
        {
            Rect safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (safe == applied && size == screenSize) return;
            applied = safe;
            screenSize = size;

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            rt.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
