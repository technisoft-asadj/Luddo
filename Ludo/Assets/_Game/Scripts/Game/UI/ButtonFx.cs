using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ludo.Game
{
    /// <summary>
    /// Makes a button feel physical: it squashes when pressed and springs back, plays its click sound, and can
    /// optionally pulse gently to invite a tap. Uses unscaled time so buttons still animate while the game is
    /// paused (Time.timeScale = 0).
    /// </summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] float pressedScale = 0.93f;
        [SerializeField] float speed = 18f;
        [SerializeField] bool pulse;
        [SerializeField] float pulseAmount = 0.035f;
        [SerializeField] SfxId sound = SfxId.Click;        // Back for the back arrows

        bool pressed;
        float current = 1f;

        void Awake()
        {
            var button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => AudioService.Play(sound));
        }

        void OnEnable()
        {
            current = 1f;
            pressed = false;
            transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        public void OnPointerExit(PointerEventData eventData) => pressed = false;

        void Update()
        {
            float target = pressed ? pressedScale : 1f;
            if (pulse && !pressed) target += pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(current, current, 1f);
        }
    }
}
