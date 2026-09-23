using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// The microphone button in the game screen (online matches only): tap to mute / unmute your microphone, or to
    /// switch voice on if you skipped it in the waiting room. Hidden in offline games.
    /// </summary>
    public sealed class VoiceHudButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] Image face;
        [SerializeField] Sprite micOn;
        [SerializeField] Sprite micOff;
        [SerializeField] Color onColor = new Color(0.22f, 0.80f, 0.32f);
        [SerializeField] Color offColor = new Color(0.90f, 0.94f, 1f);
        [SerializeField] Color mutedColor = new Color(0.96f, 0.30f, 0.32f);

        void Start()
        {
            gameObject.SetActive(GameSession.IsOnline);
            button.onClick.AddListener(OnTap);
        }

        void OnEnable()
        {
            VoiceService.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => VoiceService.Changed -= Refresh;

        async void OnTap()
        {
            if (VoiceService.IsOn) VoiceService.SetMicMuted(!VoiceService.MicMuted);
            else await VoiceService.JoinAsync(RoomService.Code);
        }

        void Refresh()
        {
            bool on = VoiceService.IsOn;
            bool muted = on && VoiceService.MicMuted;
            icon.sprite = on && !muted ? micOn : micOff;
            face.color = !on ? offColor : muted ? mutedColor : onColor;
            icon.color = !on ? new Color(0.08f, 0.18f, 0.45f) : Color.white;
        }
    }
}
