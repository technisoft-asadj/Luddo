using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// A short fade-and-grow when a screen or pop-up appears, so screens do not simply "blink" into view.
    /// Put it on a screen (or a modal); 'content' is the part that grows (defaults to this object).
    /// Uses unscaled time so pop-ups also animate while the game is paused.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFx : MonoBehaviour
    {
        [SerializeField] RectTransform content;
        [SerializeField] float seconds = 0.28f;
        [SerializeField] float startScale = 0.92f;

        CanvasGroup group;
        float time;

        void OnEnable()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            time = 0f;
            Apply(0f);
        }

        void Update()
        {
            if (time >= seconds) return;
            time += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(time / seconds));
        }

        void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);                       // ease-out: fast start, gentle stop
            group.alpha = eased;
            float overshoot = eased + Mathf.Sin(t * Mathf.PI) * 0.03f;              // tiny bounce past 1, then settle
            float s = Mathf.LerpUnclamped(startScale, 1f, overshoot);
            var target = content != null ? content : (RectTransform)transform;
            target.localScale = new Vector3(s, s, 1f);
        }
    }
}
