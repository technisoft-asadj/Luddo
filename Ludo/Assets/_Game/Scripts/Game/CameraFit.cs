using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// Zooms the camera so the whole board always fits the screen width, on any phone shape
    /// (tall 20:9, classic 16:9, tablets). Orthographic size = half of the visible height.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFit : MonoBehaviour
    {
        [SerializeField] float boardUnits = 15f;   // the board is 15 cells wide and 15 cells tall
        [SerializeField] float margin = 0.5f;      // empty space left and right, in cells

        Camera cam;

        void LateUpdate()
        {
            if (cam == null) cam = GetComponent<Camera>();
            float halfWidth = boardUnits * 0.5f + margin;
            float sizeForWidth = halfWidth / cam.aspect;                  // visible half-height needed to fit the width
            cam.orthographicSize = Mathf.Max(sizeForWidth, halfWidth);    // in landscape, make sure the height fits too
        }
    }
}
