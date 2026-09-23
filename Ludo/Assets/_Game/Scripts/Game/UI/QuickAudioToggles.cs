using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Game
{
    /// <summary>
    /// The two round buttons on the main menu that mute/unmute the music and the sound effects in one tap.
    /// "Mute" simply sets the saved volume to 0 and remembers the old level; the Settings sliders stay the master control.
    /// </summary>
    public sealed class QuickAudioToggles : MonoBehaviour
    {
        [SerializeField] Button musicButton;
        [SerializeField] Image musicIcon;
        [SerializeField] Button sfxButton;
        [SerializeField] Image sfxIcon;
        [SerializeField] Sprite musicOn, musicOff, sfxOn, sfxOff;

        static float lastMusic = 0.7f;
        static float lastSfx = 0.8f;

        void Awake()
        {
            musicButton.onClick.AddListener(ToggleMusic);
            sfxButton.onClick.AddListener(ToggleSfx);
        }

        void OnEnable()
        {
            GameSettings.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => GameSettings.Changed -= Refresh;

        void ToggleMusic()
        {
            if (GameSettings.MusicVolume > 0.001f) { lastMusic = GameSettings.MusicVolume; GameSettings.MusicVolume = 0f; }
            else GameSettings.MusicVolume = lastMusic;
        }

        void ToggleSfx()
        {
            if (GameSettings.SfxVolume > 0.001f) { lastSfx = GameSettings.SfxVolume; GameSettings.SfxVolume = 0f; }
            else GameSettings.SfxVolume = lastSfx;
        }

        void Refresh()
        {
            bool music = GameSettings.MusicVolume > 0.001f;
            bool sfx = GameSettings.SfxVolume > 0.001f;
            musicIcon.sprite = music ? musicOn : musicOff;
            sfxIcon.sprite = sfx ? sfxOn : sfxOff;
        }
    }
}
