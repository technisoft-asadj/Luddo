using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// Slowly moves a decoration (floating dice in the menu background, falling confetti on the win screen) and
    /// wraps it round when it leaves the parent, so the scene always feels alive. Pure decoration, no game logic.
    /// </summary>
    public sealed class Drift : MonoBehaviour
    {
        [SerializeField] Vector2 velocity = new Vector2(0f, 40f);   // canvas units per second
        [SerializeField] float spin = 10f;                          // degrees per second
        [SerializeField] float sway;                                // side-to-side wobble amount (canvas units)
        [SerializeField] float swaySpeed = 1f;

        RectTransform rt;
        RectTransform parent;
        float baseX;
        float phase;

        void OnEnable()
        {
            rt = (RectTransform)transform;
            parent = rt.parent as RectTransform;
            baseX = rt.anchoredPosition.x;
            phase = Random.value * 6.28f;
        }

        void Update()
        {
            if (parent == null) return;
            float dt = Time.unscaledDeltaTime;
            Vector2 p = rt.anchoredPosition;
            baseX += velocity.x * dt;
            p.y += velocity.y * dt;
            p.x = baseX + (sway > 0f ? Mathf.Sin(Time.unscaledTime * swaySpeed + phase) * sway : 0f);

            Rect r = parent.rect;
            float margin = 150f;
            if (p.y > r.height * 0.5f + margin) p.y = -r.height * 0.5f - margin;
            else if (p.y < -r.height * 0.5f - margin) p.y = r.height * 0.5f + margin;
            if (baseX > r.width * 0.5f + margin) baseX = -r.width * 0.5f - margin;
            else if (baseX < -r.width * 0.5f - margin) baseX = r.width * 0.5f + margin;

            rt.anchoredPosition = p;
            rt.Rotate(0f, 0f, spin * dt);
        }
    }
}
