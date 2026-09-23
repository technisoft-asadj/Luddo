using UnityEngine;

namespace Ludo.Game
{
    /// <summary>Gently floats and breathes an element (the logo), so a still screen never looks frozen.</summary>
    public sealed class Bob : MonoBehaviour
    {
        [SerializeField] float height = 12f;      // canvas units up and down
        [SerializeField] float speed = 1.6f;
        [SerializeField] float breathe = 0.02f;   // scale change

        RectTransform rt;
        Vector2 home;

        void OnEnable()
        {
            rt = (RectTransform)transform;
            home = rt.anchoredPosition;
        }

        void OnDisable()
        {
            if (rt != null) { rt.anchoredPosition = home; rt.localScale = Vector3.one; }
        }

        void Update()
        {
            float s = Mathf.Sin(Time.unscaledTime * speed);
            rt.anchoredPosition = home + new Vector2(0f, s * height);
            float scale = 1f + Mathf.Sin(Time.unscaledTime * speed * 0.5f) * breathe;
            rt.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
