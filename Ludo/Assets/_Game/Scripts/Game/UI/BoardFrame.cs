using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// An invisible UI rectangle that always covers exactly the board on the screen. The four player badges are
    /// anchored to its corners, so they stay next to "their" corner of the board on every phone shape.
    /// It converts the board's world corners to screen pixels, then to canvas units.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardFrame : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] Canvas canvas;
        [SerializeField] float boardUnits = 15f;

        void LateUpdate()
        {
            if (cam == null || canvas == null || canvas.scaleFactor <= 0f) return;
            float half = boardUnits * 0.5f;
            Vector3 topLeft = cam.WorldToScreenPoint(new Vector3(-half, half, 0f));
            Vector3 bottomRight = cam.WorldToScreenPoint(new Vector3(half, -half, 0f));

            float s = canvas.scaleFactor;   // screen pixels per canvas unit
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 centre = ((Vector2)(topLeft + bottomRight) * 0.5f - new Vector2(Screen.width, Screen.height) * 0.5f) / s;
            rt.anchoredPosition = centre;
            rt.sizeDelta = new Vector2(bottomRight.x - topLeft.x, topLeft.y - bottomRight.y) / s;
        }
    }
}
