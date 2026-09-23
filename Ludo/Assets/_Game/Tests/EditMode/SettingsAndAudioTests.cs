using NUnit.Framework;
using UnityEngine;
using Ludo.Game;

namespace Ludo.Tests
{
    /// <summary>The saved settings and the sound library lookup. PlayerPrefs are backed up and restored so the tests never change your real settings.</summary>
    public class SettingsAndAudioTests
    {
        static readonly string[] Keys = { "ludo.music", "ludo.sfx", "ludo.vibration", "ludo.name.0", "ludo.name.1", "ludo.avatar.0", "ludo.avatar.1" };
        static bool IsInt(string key) => key == "ludo.vibration" || key.StartsWith("ludo.avatar");
        readonly System.Collections.Generic.Dictionary<string, string> backup = new System.Collections.Generic.Dictionary<string, string>();

        [SetUp]
        public void Backup()
        {
            backup.Clear();
            foreach (var k in Keys)
            {
                if (!PlayerPrefs.HasKey(k)) continue;
                if (k.StartsWith("ludo.name")) backup[k] = PlayerPrefs.GetString(k);
                else if (IsInt(k)) backup[k] = PlayerPrefs.GetInt(k).ToString();     // stored as an int
                else backup[k] = PlayerPrefs.GetFloat(k).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
        }

        [TearDown]
        public void Restore()
        {
            foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
            foreach (var pair in backup)
            {
                if (pair.Key.StartsWith("ludo.name")) PlayerPrefs.SetString(pair.Key, pair.Value);
                else if (IsInt(pair.Key)) PlayerPrefs.SetInt(pair.Key, int.Parse(pair.Value));
                else PlayerPrefs.SetFloat(pair.Key, float.Parse(pair.Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void Volumes_HaveSensibleDefaults()
        {
            Assert.AreEqual(0.7f, GameSettings.MusicVolume, 0.0001f);
            Assert.AreEqual(0.8f, GameSettings.SfxVolume, 0.0001f);
            Assert.IsTrue(GameSettings.Vibration);
        }

        [Test]
        public void Volume_IsClampedToZeroOne()
        {
            GameSettings.MusicVolume = 5f;
            Assert.AreEqual(1f, GameSettings.MusicVolume, 0.0001f);
            GameSettings.SfxVolume = -3f;
            Assert.AreEqual(0f, GameSettings.SfxVolume, 0.0001f);
        }

        [Test]
        public void ChangingASetting_RaisesChangedEvent()
        {
            int calls = 0;
            System.Action handler = () => calls++;
            GameSettings.Changed += handler;
            try
            {
                GameSettings.MusicVolume = 0.2f;
                GameSettings.SfxVolume = 0.3f;
                GameSettings.Vibration = false;
            }
            finally { GameSettings.Changed -= handler; }
            Assert.AreEqual(3, calls);
            Assert.IsFalse(GameSettings.Vibration);
        }

        [Test]
        public void PlayerName_DefaultsAndTrims()
        {
            Assert.AreEqual("Player 1", GameSettings.PlayerName(0));
            GameSettings.SetPlayerName(0, "  Sam  ");
            Assert.AreEqual("Sam", GameSettings.PlayerName(0));
            GameSettings.SetPlayerName(1, "   ");
            Assert.AreEqual("Player 2", GameSettings.PlayerName(1));
        }

        [Test]
        public void Avatars_EachPlayerStartsWithADifferentPicture()
        {
            Assume.That(AvatarLibrary.Count, Is.GreaterThanOrEqualTo(4), "Run Ludo > Setup Avatar Library first");
            Assert.AreNotEqual(GameSettings.AvatarIndex(0), GameSettings.AvatarIndex(1));
        }

        [Test]
        public void Avatar_ChoiceIsSavedAndAlwaysInRange()
        {
            Assume.That(AvatarLibrary.Count, Is.GreaterThan(0), "Run Ludo > Setup Avatar Library first");
            GameSettings.SetAvatarIndex(0, 7);
            Assert.AreEqual(7 % AvatarLibrary.Count, GameSettings.AvatarIndex(0));
            GameSettings.SetAvatarIndex(1, 9999);              // a bad or old saved number must never break the game
            Assert.That(GameSettings.AvatarIndex(1), Is.InRange(0, AvatarLibrary.Count - 1));
            Assert.IsNotNull(AvatarLibrary.Get(-3));           // negative numbers wrap round too
            Assert.IsNotNull(AvatarLibrary.Computer);
        }

        [Test]
        public void ChangingProfile_RaisesChangedEvent()
        {
            int calls = 0;
            System.Action handler = () => calls++;
            GameSettings.Changed += handler;
            try
            {
                GameSettings.SetPlayerName(0, "Sam");
                GameSettings.SetAvatarIndex(0, 2);
            }
            finally { GameSettings.Changed -= handler; }
            Assert.AreEqual(2, calls);                          // the main-menu profile tag refreshes from this
        }

        [Test]
        public void AudioLibrary_FindsConfiguredEntriesOnly()
        {
            var lib = ScriptableObject.CreateInstance<AudioLibrary>();
            try
            {
                lib.sfx = new[]
                {
                    new AudioLibrary.Entry { id = SfxId.Capture, volume = 0.5f },
                    null    // a hole in the list must not crash the lookup
                };
                Assert.IsTrue(lib.TryGet(SfxId.Capture, out var entry));
                Assert.AreEqual(0.5f, entry.volume, 0.0001f);
                Assert.IsFalse(lib.TryGet(SfxId.Win, out _));
            }
            finally { Object.DestroyImmediate(lib); }
        }
    }
}
