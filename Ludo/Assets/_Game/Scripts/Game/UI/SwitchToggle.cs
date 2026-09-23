using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Game
{
    /// <summary>
    /// Draws a Toggle as a modern on/off switch: the knob slides and the track changes colour.
    /// It only READS the Toggle every frame, so it works whichever script changes the value.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public sealed class SwitchToggle : MonoBehaviour
    {
        [SerializeField] RectTransform knob;
        [SerializeField] Image track;
        [SerializeField] float travel = 42f;
        [SerializeField] Color onColor = new Color(0.25f, 0.8f, 0.35f);
        [SerializeField] Color offColor = new Color(0.62f, 0.68f, 0.8f);

        Toggle toggle;

        void Awake() => toggle = GetComponent<Toggle>();

        void Update()
        {
            bool on = toggle.isOn;
            float target = on ? travel : -travel;
            Vector2 p = knob.anchoredPosition;
            p.x = Mathf.Lerp(p.x, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            knob.anchoredPosition = p;
            track.color = Color.Lerp(track.color, on ? onColor : offColor, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
        }
    }
}
