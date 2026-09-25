using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// Phones are taller than the 16:9 layout the screens are drawn for, which used to leave the buttons in a clump at the
    /// top and a hole underneath. This spreads the extra height between the items: each one moves down by its weight times
    /// the spare height (0 = stays, 1 = moves all of it), so spacing between buttons grows instead. Items are top-anchored.
    /// </summary>
    public sealed class VerticalSpread : MonoBehaviour
    {
        [SerializeField] RectTransform[] items;
        [SerializeField] float[] weights;
        [SerializeField] float baseHeight = 1920f;      // the height the layout was drawn for

        Vector2[] home;
        float applied = -1f;

        void Capture()
        {
            home = new Vector2[items.Length];
            for (int i = 0; i < items.Length; i++) home[i] = items[i].anchoredPosition;
        }

        void OnEnable() => Apply(true);

        /// <summary>Where item i sits before any spreading.</summary>
        public Vector2 Home(int i)
        {
            if (home == null) Capture();
            return home[i];
        }

        public float Weight(int i) => i < weights.Length ? weights[i] : 0f;

        public void SetWeight(int i, float w)
        {
            if (i < weights.Length) weights[i] = w;
        }

        /// <summary>Move item i's starting position (the screen re-arranged its rows); the spread is applied again from there.</summary>
        public void SetHome(int i, Vector2 position)
        {
            if (home == null) Capture();
            home[i] = position;
            Apply(true);
        }

        void LateUpdate() => Apply(false);

        void Apply(bool force)
        {
            if (items == null || items.Length == 0) return;
            if (home == null) Capture();
            float extra = Mathf.Max(0f, ((RectTransform)transform).rect.height - baseHeight);
            if (!force && Mathf.Approximately(extra, applied)) return;
            applied = extra;
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null) items[i].anchoredPosition = home[i] + new Vector2(0f, -extra * (i < weights.Length ? weights[i] : 0f));
        }
    }
}
