using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ludo.Game;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Creates Resources/AudioLibrary.asset (which sound plays for which event) from the imported audio files and
    /// applies sensible import settings: short effects are decompressed on load (instant, no delay), the music is
    /// streamed from disk (does not sit in memory). Safe to run again: clips you changed by hand are kept.
    /// </summary>
    public static class AudioSetup
    {
        const string Kenney = "Assets/ThirdParty/Kenney/Audio/";
        const string MusicPath = "Assets/ThirdParty/OpenGameArt/Audio/HappyClappyLoop.wav";
        const string LibraryFolder = "Assets/_Game/Resources";
        const string LibraryPath = LibraryFolder + "/AudioLibrary.asset";

        [MenuItem("Ludo/Setup Audio Library")]
        public static void Run()
        {
            // 1. import settings
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/ThirdParty" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ConfigureClip(path, path == MusicPath);
            }

            // 2. the library asset
            Directory.CreateDirectory(LibraryFolder);
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            bool isNew = lib == null;
            if (isNew) lib = ScriptableObject.CreateInstance<AudioLibrary>();

            var existing = new Dictionary<SfxId, AudioLibrary.Entry>();
            foreach (var e in lib.sfx) if (e != null) existing[e.id] = e;

            var list = new List<AudioLibrary.Entry>();
            Add(list, existing, SfxId.Click, 0.8f, 0.95f, 1.05f, Kenney + "UI/click_001.ogg");
            Add(list, existing, SfxId.Back, 0.8f, 0.95f, 1.05f, Kenney + "UI/back_001.ogg");
            Add(list, existing, SfxId.DiceRoll, 1f, 0.95f, 1.05f, Kenney + "Dice/dice-shake-1.ogg");
            Add(list, existing, SfxId.DiceLand, 1f, 0.95f, 1.05f, Kenney + "Dice/dice-throw-1.ogg", Kenney + "Dice/dice-throw-2.ogg");
            Add(list, existing, SfxId.Step, 0.55f, 0.9f, 1.15f, Kenney + "Board/chip-lay-1.ogg");
            Add(list, existing, SfxId.Capture, 1f, 0.95f, 1.05f, Kenney + "Board/chips-collide-1.ogg");
            Add(list, existing, SfxId.Home, 0.9f, 1f, 1f, Kenney + "UI/confirmation_001.ogg");
            Add(list, existing, SfxId.Error, 0.7f, 1f, 1f, Kenney + "UI/error_001.ogg");

            string jingleReport = AddWinJingle(list, existing);
            lib.sfx = list.ToArray();
            if (lib.music == null) lib.music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            if (isNew) { lib.musicGain = 0.5f; AssetDatabase.CreateAsset(lib, LibraryPath); }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ludo] Audio library ready: " + LibraryPath + "\n" + jingleReport);
        }

        static void ConfigureClip(string path, bool isMusic)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            var s = importer.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = isMusic ? 0.5f : 0.7f;
            s.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = !isMusic;                                    // effects are ready before the first tap
            importer.defaultSampleSettings = s;
            importer.forceToMono = !isMusic && !path.Contains("/Jingles/");   // effects are plain 2D: mono halves the memory
            importer.loadInBackground = isMusic;
            importer.SaveAndReimport();
        }

        static void Add(List<AudioLibrary.Entry> list, Dictionary<SfxId, AudioLibrary.Entry> existing,
            SfxId id, float volume, float pitchMin, float pitchMax, params string[] paths)
        {
            if (existing.TryGetValue(id, out var kept) && kept.clips != null && kept.clips.Length > 0)
            {
                list.Add(kept);      // the user changed this one by hand: leave it
                return;
            }
            var clips = new List<AudioClip>();
            foreach (var p in paths)
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (c == null) Debug.LogError("[Ludo] Missing audio clip: " + p);
                else clips.Add(c);
            }
            list.Add(new AudioLibrary.Entry { id = id, clips = clips.ToArray(), volume = volume, pitch = new Vector2(pitchMin, pitchMax) });
        }

        /// <summary>Picks a default win jingle: the one whose length is closest to 2.5 seconds. Change it in the AudioLibrary asset any time.</summary>
        static string AddWinJingle(List<AudioLibrary.Entry> list, Dictionary<SfxId, AudioLibrary.Entry> existing)
        {
            var report = new StringBuilder("Jingle lengths (seconds):");
            AudioClip best = null;
            float bestDistance = float.MaxValue;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Kenney + "Jingles" }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                report.Append("\n  ").Append(clip.name).Append("  ").Append(clip.length.ToString("0.00"));
                float d = Mathf.Abs(clip.length - 2.5f);
                if (d < bestDistance) { bestDistance = d; best = clip; }
            }
            if (existing.TryGetValue(SfxId.Win, out var kept) && kept.clips != null && kept.clips.Length > 0)
            {
                list.Add(kept);
                report.Append("\nWin jingle kept: ").Append(kept.clips[0].name);
            }
            else
            {
                list.Add(new AudioLibrary.Entry { id = SfxId.Win, clips = new[] { best }, volume = 1f, pitch = new Vector2(1f, 1f) });
                report.Append("\nWin jingle chosen: ").Append(best != null ? best.name : "none");
            }
            return report.ToString();
        }
    }
}
