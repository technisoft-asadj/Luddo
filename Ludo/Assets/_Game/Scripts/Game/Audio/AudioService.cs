using System.Collections;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// The one place that plays sound. Game code says AudioService.Play(SfxId.Capture) and never touches an AudioSource.
    /// It creates itself before the first scene loads (from Resources/AudioLibrary), survives scene changes, keeps one
    /// music source and a small pool of effect sources, and follows the saved volumes live (GameSettings.Changed).
    /// If the library is missing, every call quietly does nothing - the game still works without sound.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int SfxVoices = 8;
        const float MinRepeatSeconds = 0.03f;      // the same effect cannot fire twice within this time

        static AudioService instance;

        AudioLibrary library;
        AudioSource music;
        AudioSource[] voices;
        int nextVoice;
        float[] lastPlayed;
        int[] lastVoice;                           // which voice last played each effect (so it can be stopped)
        float duck = 1f;                           // 1 = music at full slider level, lower while a jingle plays
        bool musicStarted;
        Coroutine duckRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            instance = null;   // statics can survive between Editor play sessions
            var lib = Resources.Load<AudioLibrary>("AudioLibrary");
            if (lib == null)
            {
                Debug.LogWarning("[Ludo] Resources/AudioLibrary is missing - the game will run silently. Run Ludo > Setup Audio Library.");
                return;
            }
            var go = new GameObject("AudioService");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioService>();
            instance.Init(lib);
        }

        // ---------- public API ----------

        public static void Play(SfxId id)
        {
            if (instance != null) instance.PlayInternal(id);
        }

        /// <summary>Cut a long effect short (the dice shake ends when the dice lands).</summary>
        public static void Stop(SfxId id)
        {
            if (instance != null) instance.StopInternal(id);
        }

        /// <summary>Lower the music for a moment (while the win jingle plays), then bring it back.</summary>
        public static void DuckMusic(float seconds)
        {
            if (instance != null) instance.StartDuck(seconds);
        }

        // ---------- setup ----------

        void Init(AudioLibrary lib)
        {
            library = lib;
            music = NewSource("Music");
            music.loop = true;
            music.clip = lib.music;
            voices = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++) voices[i] = NewSource("Sfx" + i);
            lastPlayed = new float[System.Enum.GetValues(typeof(SfxId)).Length];
            lastVoice = new int[lastPlayed.Length];
            for (int i = 0; i < lastPlayed.Length; i++) { lastPlayed[i] = -10f; lastVoice[i] = -1; }

            GameSettings.Changed += ApplyVolumes;
            ApplyVolumes();
        }

        AudioSource NewSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var s = child.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;     // plain 2D sound
            return s;
        }

        void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolumes;
            if (instance == this) instance = null;
        }

        // ---------- volumes and music ----------

        void ApplyVolumes()
        {
            float level = GameSettings.MusicVolume;
            music.volume = level * library.musicGain * duck;
            if (library.music == null) return;

            if (level <= 0.001f)
            {
                if (music.isPlaying) music.Pause();          // silent music needs no decoding
            }
            else if (!musicStarted)
            {
                musicStarted = true;
                music.Play();
            }
            else if (!music.isPlaying)
            {
                music.UnPause();
            }
        }

        void StartDuck(float seconds)
        {
            if (duckRoutine != null) StopCoroutine(duckRoutine);
            duckRoutine = StartCoroutine(DuckRoutine(seconds));
        }

        IEnumerator DuckRoutine(float seconds)
        {
            duck = 0.25f;
            ApplyVolumes();
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)       // fade the music back in over one second
            {
                duck = Mathf.Lerp(0.25f, 1f, t);
                ApplyVolumes();
                yield return null;
            }
            duck = 1f;
            ApplyVolumes();
            duckRoutine = null;
        }

        // ---------- effects ----------

        void PlayInternal(SfxId id)
        {
            float sfxLevel = GameSettings.SfxVolume;
            if (sfxLevel <= 0.001f) return;
            if (!library.TryGet(id, out var entry) || entry.clips == null || entry.clips.Length == 0) return;

            int index = (int)id;
            if (Time.unscaledTime - lastPlayed[index] < MinRepeatSeconds) return;
            lastPlayed[index] = Time.unscaledTime;

            var clip = entry.clips[Random.Range(0, entry.clips.Length)];
            if (clip == null) return;

            lastVoice[index] = nextVoice;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;       // round robin: the oldest voice is reused first
            voice.pitch = Random.Range(entry.pitch.x, entry.pitch.y);
            voice.volume = entry.volume * sfxLevel;
            voice.clip = clip;
            voice.Play();
        }

        void StopInternal(SfxId id)
        {
            int v = lastVoice[(int)id];
            if (v < 0) return;
            var voice = voices[v];
            if (!voice.isPlaying || !library.TryGet(id, out var entry)) return;
            foreach (var c in entry.clips)
                if (c == voice.clip) { voice.Stop(); return; }     // only if that voice still plays THIS effect
        }
    }
}
