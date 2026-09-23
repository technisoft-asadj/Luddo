using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Game
{
    /// <summary>The short in-game messages. Each kind has its own colour, icon and wording.</summary>
    public enum ToastKind { NoMoves, ThreeSixes, Captured, RollAgain }

    /// <summary>
    /// A coloured banner across the board that pops in, holds and fades away ("No Moves", "Three Sixes! Turn lost",
    /// "Captured!"). It only DISPLAYS a message it is told about; GameController decides when. Uses unscaled time.
    /// </summary>
    public sealed class ToastBanner : MonoBehaviour
    {
        [SerializeField] RectTransform panel;        // the banner itself (this is what pops and shakes)
        [SerializeField] CanvasGroup group;
        [SerializeField] Image body;                 // coloured face; its first Shadow component is the darker lip
        [SerializeField] Shadow lip;
        [SerializeField] Image icon;
        [SerializeField] GameObject iconBadge;       // round badge holding the single icon
        [SerializeField] GameObject sixes;           // three dice showing 6, used for "Three sixes"
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] Sprite crossIcon;
        [SerializeField] Sprite captureIcon;         // a return arrow: the token goes back to base
        [SerializeField] Sprite diceIcon;            // a dice showing 6: "Six! Roll again"

        Coroutine routine;

        void Awake() => HideNow();

        public void Show(ToastKind kind, float holdSeconds)
        {
            if (routine != null) StopCoroutine(routine);
            Apply(kind);
            routine = StartCoroutine(Play(kind, holdSeconds));
        }

        public void Hide()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            HideNow();
        }

        void HideNow()
        {
            group.alpha = 0f;
            panel.gameObject.SetActive(false);
        }

        void Apply(ToastKind kind)
        {
            Color face, edge;
            string head, sub;
            switch (kind)
            {
                case ToastKind.ThreeSixes:
                    face = new Color(0.96f, 0.30f, 0.32f); edge = new Color(0.62f, 0.10f, 0.14f);
                    head = "Three Sixes!"; sub = "Turn lost";
                    break;
                case ToastKind.Captured:
                    face = new Color(0.22f, 0.80f, 0.32f); edge = new Color(0.08f, 0.45f, 0.15f);
                    head = "Captured!"; sub = "Sent back to base";
                    break;
                case ToastKind.RollAgain:
                    face = new Color(0.22f, 0.58f, 1f); edge = new Color(0.07f, 0.30f, 0.72f);
                    head = "Six!"; sub = "Roll again";
                    break;
                default:
                    face = new Color(1f, 0.68f, 0.14f); edge = new Color(0.78f, 0.38f, 0.04f);
                    head = "No Moves"; sub = "Turn passes on";
                    break;
            }
            body.color = face;
            lip.effectColor = edge;
            title.text = head;
            subtitle.text = sub;

            bool dice = kind == ToastKind.ThreeSixes;
            sixes.SetActive(dice);
            iconBadge.SetActive(!dice);
            icon.sprite = kind == ToastKind.Captured ? captureIcon : kind == ToastKind.RollAgain && diceIcon != null ? diceIcon : crossIcon;
        }

        IEnumerator Play(ToastKind kind, float hold)
        {
            panel.gameObject.SetActive(true);
            Vector2 home = Vector2.zero;
            panel.anchoredPosition = home;

            // pop in: grows past full size and settles (ease-out-back)
            const float inTime = 0.28f;
            for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime)
            {
                float k = t / inTime;
                float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
                float s = Mathf.LerpUnclamped(0.55f, 1f, back);
                panel.localScale = new Vector3(s, s, 1f);
                group.alpha = Mathf.Clamp01(k * 2f);
                yield return null;
            }
            panel.localScale = Vector3.one;
            group.alpha = 1f;

            // hold; a lost turn shakes a little so it feels like a penalty
            float holdTime = Mathf.Max(0.2f, hold - inTime - 0.22f);
            for (float t = 0f; t < holdTime; t += Time.unscaledDeltaTime)
            {
                float shake = kind == ToastKind.ThreeSixes && t < 0.35f ? Mathf.Sin(t * 70f) * 14f * (1f - t / 0.35f) : 0f;
                panel.anchoredPosition = home + new Vector2(shake, 0f);
                yield return null;
            }
            panel.anchoredPosition = home;

            // fade out and drift up
            const float outTime = 0.22f;
            for (float t = 0f; t < outTime; t += Time.unscaledDeltaTime)
            {
                float k = t / outTime;
                group.alpha = 1f - k;
                panel.anchoredPosition = home + new Vector2(0f, 40f * k);
                float s = Mathf.Lerp(1f, 0.92f, k);
                panel.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            HideNow();
            routine = null;
        }
    }
}
