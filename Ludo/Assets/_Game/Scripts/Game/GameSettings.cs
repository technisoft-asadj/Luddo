using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// The player's saved preferences. Backed by PlayerPrefs (a small key-value store the phone keeps for the app),
    /// which is plenty for a few numbers and names. AudioService listens to Changed, so a slider moves the music at once.
    /// </summary>
    public static class GameSettings
    {
        const string MusicKey = "ludo.music";
        const string SfxKey = "ludo.sfx";
        const string VibrationKey = "ludo.vibration";
        const string NameKey = "ludo.name.";
        const string AvatarKey = "ludo.avatar.";
        const string CountryKey = "ludo.country";
        const string DiceSkinKey = "ludo.dice";
        const string CountryAskedKey = "ludo.country.asked";

        /// <summary>Raised after music, effects or vibration settings change.</summary>
        public static event System.Action Changed;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.7f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            set { PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Test builds only (DevAutoRun -name X): the name of player 0 without touching the saved preferences, which all test clients on one computer share.</summary>
        public static string DevName = "";

        /// <summary>Test builds only (DevAutoRun -country PK): this client's country without touching the shared saved preferences.</summary>
        public static string DevCountry = null;
#endif

        public static string PlayerName(int index)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (index == 0 && !string.IsNullOrEmpty(DevName)) return DevName;
#endif
            return PlayerPrefs.GetString(NameKey + index, "Player " + (index + 1));
        }

        public static void SetPlayerName(int index, string name)
        {
            name = string.IsNullOrWhiteSpace(name) ? "Player " + (index + 1) : name.Trim();
            PlayerPrefs.SetString(NameKey + index, name);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Back to the default name and avatar (the account that filled them in is gone from this phone).</summary>
        public static void ResetPlayer(int index)
        {
            PlayerPrefs.DeleteKey(NameKey + index);
            PlayerPrefs.DeleteKey(AvatarKey + index);
            if (index == 0) { PlayerPrefs.DeleteKey(CountryKey); PlayerPrefs.DeleteKey(CountryAskedKey); }
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Which avatar picture (0..AvatarLibrary.Count-1) this player chose. Each player starts with a different one.</summary>
        public static int AvatarIndex(int index)
        {
            int count = Mathf.Max(1, AvatarLibrary.Count);
            int saved = PlayerPrefs.GetInt(AvatarKey + index, index);
            return ((saved % count) + count) % count;
        }

        public static void SetAvatarIndex(int index, int avatar)
        {
            PlayerPrefs.SetInt(AvatarKey + index, Mathf.Max(0, avatar));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>The phone owner's country (ISO code such as "PK"), chosen by the player in their profile; "" = not chosen (no flag).</summary>
        public static string Country
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DevCountry != null) return Ludo.Core.Countries.Normalize(DevCountry);
#endif
                return Ludo.Core.Countries.Normalize(PlayerPrefs.GetString(CountryKey, ""));
            }
            set
            {
                string code = Ludo.Core.Countries.Normalize(value);
                if (code == Country) return;
                PlayerPrefs.SetString(CountryKey, code);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// The dice design this phone rolls with. Which designs the player actually owns is decided by their profile
        /// (level, rank, coins - see Ludo.Core.DiceSkins); this only remembers the choice so the Game scene can read it
        /// without knowing anything about the online layer, and so an offline game shows the same dice.
        /// </summary>
        public static string DiceSkin
        {
            get
            {
                string id = PlayerPrefs.GetString(DiceSkinKey, Ludo.Core.DiceSkins.Default);
                return Ludo.Core.DiceSkins.Exists(id) ? id : Ludo.Core.DiceSkins.Default;
            }
            set
            {
                string id = Ludo.Core.DiceSkins.Exists(value) ? value : Ludo.Core.DiceSkins.Default;
                if (id == DiceSkin) return;
                PlayerPrefs.SetString(DiceSkinKey, id);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>The country list was offered once to the player on this phone (so it is not pushed again).</summary>
        public static bool CountryAsked
        {
            get => PlayerPrefs.GetInt(CountryAskedKey, 0) == 1;
            set { PlayerPrefs.SetInt(CountryAskedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>The picture to show for this local player: their own photo (player 0 only, the phone's owner) or the avatar they chose.</summary>
        public static Sprite Picture(int index) =>
            index == 0 && ProfilePhoto.Mine != null ? ProfilePhoto.Mine : AvatarLibrary.Get(AvatarIndex(index));

        /// <summary>Tell listeners (menus, cloud sync) that the profile changed, for changes that are not stored here (the photo).</summary>
        public static void NotifyChanged() => Changed?.Invoke();
    }
}
