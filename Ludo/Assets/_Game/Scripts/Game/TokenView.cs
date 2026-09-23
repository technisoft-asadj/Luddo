using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// The picture of ONE token. It knows which (player, token) of the game it shows, and how to slide or hop
    /// to a position. It never decides where it should be - BoardView tells it.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TokenView : MonoBehaviour
    {
        const int SortingBase = 1000;   // must stay above the board picture (order 0) for every y position

        SpriteRenderer sprite;
        SpriteRenderer shadow, outline, gloss;     // untinted layers around the coloured body
        Vector3 baseScale = Vector3.one;
        float factor = 1f;          // 1 = alone on its spot, smaller when tokens share a spot
        bool selectable;            // true while the player may tap this token

        public int Player { get; private set; }
        public int Token { get; private set; }

        public void Init(int player, int token, Sprite art, Color tint, Sprite shadowArt, Sprite outlineArt, Sprite glossArt)
        {
            Player = player;
            Token = token;
            sprite = GetComponent<SpriteRenderer>();
            sprite.sprite = art;
            sprite.color = tint;
            shadow = Layer("Shadow", shadowArt);
            outline = Layer("Outline", outlineArt);
            gloss = Layer("Gloss", glossArt);
            baseScale = transform.localScale;
        }

        SpriteRenderer Layer(string layerName, Sprite art)
        {
            if (art == null) return null;
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            return sr;
        }

        /// <summary>Tokens lower on the screen are drawn in front of tokens above them.</summary>
        void LateUpdate()
        {
            int order = SortingBase + Mathf.RoundToInt(-transform.position.y * 40f);
            sprite.sortingOrder = order;
            if (shadow != null) shadow.sortingOrder = order - 3;
            if (outline != null) outline.sortingOrder = order - 2;
            if (gloss != null) gloss.sortingOrder = order + 1;
            if (selectable) ApplyScale(1f + 0.12f * Mathf.Sin(Time.time * 9f));   // pulse = "tap me"
        }

        public void Place(Vector3 position, float scaleFactor)
        {
            transform.position = position;
            factor = scaleFactor;
            ApplyScale(1f);
        }

        public void SetSelectable(bool on)
        {
            selectable = on;
            if (!on) ApplyScale(1f);
        }

        void ApplyScale(float pulse) => transform.localScale = baseScale * (factor * pulse);

        /// <summary>Slide through the waypoints one after another (one waypoint = one board cell).</summary>
        public IEnumerator MoveAlong(IReadOnlyList<Vector3> waypoints, float secondsPerStep)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                yield return Slide(waypoints[i], secondsPerStep, 0.12f);
                AudioService.Play(SfxId.Step);   // a soft tap each time the token lands on a cell
            }
        }

        /// <summary>Jump in an arc to a spot (used when a token is captured and goes back to base).</summary>
        public IEnumerator Hop(Vector3 to, float seconds)
        {
            yield return Slide(to, seconds, 0.6f);
        }

        IEnumerator Slide(Vector3 to, float seconds, float arcHeight)
        {
            Vector3 from = transform.position;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * arcHeight;
                transform.position = p;
                yield return null;
            }
            transform.position = to;
        }
    }
}
