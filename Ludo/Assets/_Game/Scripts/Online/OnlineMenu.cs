using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// The "Play Online" screen: Ranked Match, Quick Match, private rooms (create / join with a code), Friends, Leaderboards,
    /// the Weekly Cup tournament, your profile and statistics, and your account.
    /// It connects to the online services when it opens and shows a short status line. The offline game never needs it.
    /// </summary>
    public sealed class OnlineMenu : MonoBehaviour
    {
        [SerializeField] ScreenRouter router;
        [SerializeField] int roomScreen = 9;
        [SerializeField] int friendsScreen = 10;
        [SerializeField] int accountScreen = 11;
        [SerializeField] int leaderboardScreen = 12;
        [SerializeField] int profileScreen = 13;
        [SerializeField] int quickMatchScreen = 15;
        [SerializeField] int welcomeScreen = 14;
        [SerializeField] TMP_Text profileName;           // the profile card at the top: picture, name, tier and rating
        [SerializeField] TMP_Text profileRating;         // the rank pill: "Bronze"
        [SerializeField] TMP_Text profileLevel;          // "Lv. 12"
        [SerializeField] Image profileXpBar;             // fills across the level block
        [SerializeField] TMP_Text profileXpText;         // "720 / 1000"
        [SerializeField] TMP_Text profileCoins;
        [SerializeField] Image profileAvatar;
        [SerializeField] Image profileFlag;              // my country (hidden if none chosen)
        [SerializeField] CountryPicker countryPicker;    // asked once per account when no country is chosen yet
        [SerializeField] TMP_Text statusText;
        [SerializeField] GameObject busyOverlay;         // "Connecting..." veil that blocks taps while waiting
        [SerializeField] TMP_Text busyText;
        [SerializeField] GameObject joinModal;
        [SerializeField] TMP_InputField codeInput;
        [SerializeField] TMP_Text joinError;
        [SerializeField] Image[] sizeChips;              // "2 Players", "3 Players", "4 Players": the table size for Ranked, Quick Match and Create Room
        [SerializeField] TMP_Text[] sizeLabels;
        [SerializeField] Image[] feeChips;               // coin table for Quick Match: Free / 100 / 500 / 1K / 5K (CoinTables.Fees)
        [SerializeField] TMP_Text[] feeLabels;
        [SerializeField] DailyRewardPanel daily;
        [SerializeField] DiceCollectionPanel diceCollection;
        [SerializeField] ChestPanel chest;
        [SerializeField] GameObject chestDot;            // red dot on the chest button: a free chest is ready
        [SerializeField] GameObject giftDot;             // red dot on the gift button: a daily reward is waiting

        const string SizeKey = "ludo.online.size";
        const string FeeKey = "ludo.online.fee";
        int fee;
        static readonly Color ChipOn = new Color(0.22f, 0.78f, 0.34f);
        static readonly Color ChipOff = new Color(0.93f, 0.96f, 1f);

        bool working;
        int size = 2;

        void OnEnable()
        {
            if (!OnlineService.HasChosenLogin)                // nobody is logged in: never go online by ourselves
            {
                StartCoroutine(ToLoginNextFrame());           // the router is still switching screens right now
                return;
            }
            size =Mathf.Clamp(PlayerPrefs.GetInt(SizeKey, 2), RoomService.MinSize, RoomService.MaxSize);
            RefreshSize();
            fee = PlayerPrefs.GetInt(FeeKey, 0);
            if (!Ludo.Core.CoinTables.IsFee(fee)) fee = 0;
            RefreshFee();
            joinModal.SetActive(false);
            busyOverlay.SetActive(false);
            StatsService.Changed += RefreshProfile;
            GameSettings.Changed += RefreshProfile;
            ModePicker.Changed += RefreshSize;
            RefreshProfile();
            Connect();
            AskCountryOnce();
        }

        /// <summary>
        /// The first time a logged-in player opens Play Online without a country, the country list opens once (they can close
        /// it: no flag is shown then). Never asked again for this account; the profile editor can change it any time.
        /// </summary>
        void AskCountryOnce()
        {
            if (countryPicker == null || GameSettings.Country.Length > 0 || GameSettings.CountryAsked) return;
            StartCoroutine(OpenCountryNextFrame());          // the router closes every pop-up while it is still switching screens
        }

        System.Collections.IEnumerator OpenCountryNextFrame()
        {
            yield return null;
            if (!isActiveAndEnabled || GameSettings.Country.Length > 0 || GameSettings.CountryAsked) yield break;
            GameSettings.CountryAsked = true;
            countryPicker.Open("", code => GameSettings.Country = code);
        }

        void OnDisable()
        {
            StatsService.Changed -= RefreshProfile;
            GameSettings.Changed -= RefreshProfile;
            ModePicker.Changed -= RefreshSize;
        }

        System.Collections.IEnumerator ToLoginNextFrame()
        {
            yield return null;
            LoginGate.Purpose = LoginPurpose.Online;
            router.Replace(welcomeScreen);
        }

        /// <summary>The 2 / 3 / 4 player buttons. Team Up is 2 vs 2, so its table is always four.</summary>
        public void SetSize(int players)
        {
            if (GameSession.NeedsFourPlayers(ModePicker.Current)) { RefreshSize(); return; }
            size = Mathf.Clamp(players, RoomService.MinSize, RoomService.MaxSize);
            PlayerPrefs.SetInt(SizeKey, size);
            PlayerPrefs.Save();
            RefreshSize();
        }

        /// <summary>The table size the next room / search really uses (Team Up forces four seats).</summary>
        int TableSize => GameSession.NeedsFourPlayers(ModePicker.Current) ? RoomService.MaxSize : size;

        void RefreshSize()
        {
            if (sizeChips == null) return;
            bool locked = GameSession.NeedsFourPlayers(ModePicker.Current);
            for (int i = 0; i < sizeChips.Length; i++)
            {
                bool on = i + RoomService.MinSize == TableSize;
                sizeChips[i].color = on ? ChipOn : locked ? new Color(0.86f, 0.89f, 0.95f) : ChipOff;
                if (sizeLabels != null && i < sizeLabels.Length)
                    sizeLabels[i].color = on ? Color.white : new Color(0.10f, 0.16f, 0.35f, locked ? 0.45f : 1f);
            }
        }

        /// <summary>The coin table chips (index into CoinTables.Fees).</summary>
        public void SetFee(int index)
        {
            var fees = Ludo.Core.CoinTables.Fees;
            fee = fees[Mathf.Clamp(index, 0, fees.Length - 1)];
            PlayerPrefs.SetInt(FeeKey, fee);
            PlayerPrefs.Save();
            RefreshFee();
        }

        void RefreshFee()
        {
            if (feeChips == null) return;
            var fees = Ludo.Core.CoinTables.Fees;
            for (int i = 0; i < feeChips.Length && i < fees.Length; i++)
            {
                bool on = fees[i] == fee;
                feeChips[i].color = on ? new Color(1f, 0.82f, 0.15f) : ChipOff;
                if (feeLabels != null && i < feeLabels.Length) feeLabels[i].color = new Color(0.10f, 0.16f, 0.35f);
            }
        }

        public void OpenDaily() { if (daily != null) daily.Open(); }

        /// <summary>The dice collection: wear or unlock a dice design (cosmetic only).</summary>
        public void OpenDice() { if (diceCollection != null) diceCollection.Open(); }

        /// <summary>The free chest that fills up again every few hours.</summary>
        public void OpenChest() { if (chest != null) chest.Open(); }

        bool dailyQueued;

        System.Collections.IEnumerator OpenDailyNextFrame()
        {
            yield return null;
            yield return null;
            dailyQueued = false;
            if (isActiveAndEnabled && (countryPicker == null || !countryPicker.IsOpen)) daily.OpenIfNew();
        }

        void RefreshProfile()
        {
            if (this == null || profileName == null) return;
            profileName.text = GameSettings.PlayerName(0);
            profileAvatar.sprite = GameSettings.Picture(0);
            FlagLibrary.Apply(profileFlag, GameSettings.Country);
            var s = StatsService.Mine;
            profileRating.text = s.Tier;
            if (profileLevel != null) profileLevel.text = "Lv. " + s.Level;
            if (profileCoins != null) profileCoins.text = s.coins.ToString("N0");
            if (profileXpText != null || profileXpBar != null)
            {
                long into = Ludo.Core.Progression.XpIntoLevel(s.xp);
                long need = Ludo.Core.Progression.XpToNext(s.Level);
                if (profileXpText != null) profileXpText.text = need > 0 ? into + " / " + need : "MAX";
                if (profileXpBar != null) profileXpBar.fillAmount = need > 0 ? Mathf.Clamp01((float)into / need) : 1f;
            }
            if (giftDot != null) giftDot.SetActive(DailyRewardPanel.Claimable);
            if (chestDot != null) chestDot.SetActive(ChestPanel.Claimable);
            if (daily != null && StatsService.Loaded && isActiveAndEnabled && !dailyQueued && DailyRewardPanel.Claimable)
            {
                dailyQueued = true;
                StartCoroutine(OpenDailyNextFrame());           // (the router closes pop-ups while it switches screens)
            }
        }

        async void Connect()
        {
            statusText.text = "Connecting...";
            statusText.color = new Color(1f, 1f, 1f, 0.8f);
            bool ok = await OnlineService.ConnectAsync();
            if (this == null) return;
            if (ok)
            {
                statusText.text = OnlineService.IsAccount ? (OnlineService.ProviderLabel == "Account" ? "Signed in  -  " + OnlineService.AccountName : "Signed in with " + OnlineService.ProviderLabel)
                                : "Online as guest  -  " + GameSettings.PlayerName(0);
                statusText.color = new Color(0.6f, 1f, 0.65f);
                _ = SocialService.StartAsync();                   // friends + invites work from here on
                _ = StatsService.LoadMineAsync();                 // my rating and results (the profile card updates when they arrive)
                _ = OnlineService.PushProfileNameAsync();
            }
            else if (!OnlineService.HasChosenLogin)
            {
                // the saved login was no longer valid (the phone is now logged out): the login page, then back here
                LoginGate.Purpose = LoginPurpose.Online;
                LoginGate.Notice = OnlineService.LastError;
                router.Replace(welcomeScreen);
            }
            else
            {
                statusText.text = OnlineService.LastError + "  (tap to retry)";
                statusText.color = new Color(1f, 0.75f, 0.7f);
            }
        }

        /// <summary>Tapping the status line retries the connection.</summary>
        public void Retry()
        {
            if (!OnlineService.IsReady && !working) Connect();
        }

        // ---------- buttons ----------

        /// <summary>Quick Match: the worldwide ranked search (Rank Points and XP change).</summary>
        public void QuickMatch() => OpenSearch(ranked: false);

        /// <summary>Quick Match and Ranked Match open the worldwide search screen (timer, players found, Cancel).</summary>
        void OpenSearch(bool ranked)
        {
            if (working) return;
            QuickMatchScreen.PendingRanked = ranked;
            QuickMatchScreen.PendingSize = TableSize;
            QuickMatchScreen.PendingMode = ModePicker.Current;
            QuickMatchScreen.PendingFee = fee;
            if (fee > 0 && StatsService.Loaded && !Ludo.Core.CoinTables.CanAfford(StatsService.Mine.coins, fee))
            {
                statusText.text = "Not enough coins for the " + Ludo.Core.CoinTables.Label(fee) + " table. Claim your daily reward or pick a smaller table.";
                statusText.color = new Color(1f, 0.75f, 0.7f);
                return;
            }
            router.Show(quickMatchScreen);
        }

        public void OpenLeaderboards()
        {
            LeaderboardScreen.PendingTab = 0;
            router.Show(leaderboardScreen);
        }

        /// <summary>The Weekly Cup tournament lives on the leaderboards screen, on its own tab.</summary>
        public void OpenTournament()
        {
            LeaderboardScreen.PendingTab = 1;
            router.Show(leaderboardScreen);
        }

        public void OpenProfile() => router.Show(profileScreen);

        public void CreateRoom() { int n = TableSize; var m = ModePicker.Current; Enter("Creating your room...", () => RoomService.CreatePrivateAsync(n, m)); }

        public void OpenJoin()
        {
            joinError.text = "";
            codeInput.text = "";
            joinModal.SetActive(true);
            codeInput.ActivateInputField();
        }

        public void CloseJoin() => joinModal.SetActive(false);

        public void ConfirmJoin()
        {
            string code = codeInput.text;
            Enter("Joining...", () => RoomService.JoinByCodeAsync(code), fromJoinDialog: true);
        }

        public void OpenFriends() => router.Show(friendsScreen);

        /// <summary>Account: how this phone is signed in, log in with Google / Facebook (guests), log out.</summary>
        public void OpenAccount() => router.Show(accountScreen);

        async void Enter(string message, System.Func<Task<bool>> action, bool fromJoinDialog = false)
        {
            if (working) return;
            working = true;
            busyText.text = message;
            busyOverlay.SetActive(true);
            bool ok = await action();
            if (this == null) return;
            busyOverlay.SetActive(false);
            working = false;
            if (ok)
            {
                joinModal.SetActive(false);
                router.Show(roomScreen);
            }
            else if (fromJoinDialog) joinError.text = RoomService.LastError;
            else
            {
                statusText.text = RoomService.LastError;
                statusText.color = new Color(1f, 0.75f, 0.7f);
            }
        }
    }
}
