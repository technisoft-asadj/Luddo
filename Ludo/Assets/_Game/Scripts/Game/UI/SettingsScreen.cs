using UnityEngine;
using UnityEngine.UI;
using Ludo.Services;

namespace Ludo.Game
{
    /// <summary>Connects the settings controls to the saved GameSettings. Changing a control saves it immediately,
    /// and the audio system reacts at once (music volume follows the slider while you drag it).</summary>
    public sealed class SettingsScreen : MonoBehaviour
    {
        [SerializeField] Slider music;
        [SerializeField] Slider sfx;
        [SerializeField] Toggle vibration;
        [SerializeField] Button privacyButton;      // "Privacy settings": only shown in regions where the law requires it

        float lastSfxPreview;

        void Awake()
        {
            music.onValueChanged.AddListener(v => GameSettings.MusicVolume = v);
            sfx.onValueChanged.AddListener(OnSfxChanged);
            vibration.onValueChanged.AddListener(OnVibrationChanged);
            privacyButton.onClick.AddListener(() => AdsService.ShowPrivacyOptions(null));
        }

        void OnEnable()
        {
            privacyButton.gameObject.SetActive(AdsService.PrivacyOptionsRequired);
            // show the saved values without triggering the "changed" callbacks
            music.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfx.SetValueWithoutNotify(GameSettings.SfxVolume);
            vibration.SetIsOnWithoutNotify(GameSettings.Vibration);
        }

        void OnSfxChanged(float value)
        {
            GameSettings.SfxVolume = value;
            // let the player HEAR the new level, but not on every pixel of the drag
            if (Time.unscaledTime - lastSfxPreview < 0.15f) return;
            lastSfxPreview = Time.unscaledTime;
            AudioService.Play(SfxId.Click);
        }

        void OnVibrationChanged(bool on)
        {
            GameSettings.Vibration = on;
            if (on) Haptics.Pulse(40);     // feel it straight away
        }
    }
}
