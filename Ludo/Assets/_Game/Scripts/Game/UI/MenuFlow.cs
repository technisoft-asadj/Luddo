using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.AI;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// The choices made in the menus: how many players, which names, which difficulty. It fills in GameSession and
    /// starts the Game scene. Buttons call its public methods through the Inspector.
    /// Flow:  Play > Mode  >  2/3/4 Players > Select Players > Start        (pass-and-play)
    ///                     >  vs Computer   > Select Difficulty > Start     (1 person vs 1-3 computers)
    /// </summary>
    public sealed class MenuFlow : MonoBehaviour
    {
        [SerializeField] ScreenRouter router;
        [SerializeField] int playerSelectScreen = 3;
        [SerializeField] int difficultyScreen = 4;
        [SerializeField] int onlineScreen = 8;
        [SerializeField] int welcomeScreen = 14;         // the login page, for the bottom bar's online destinations
        [SerializeField] ProfileEditor profileEditor;

        [Header("Select Players screen")]
        [SerializeField] GameObject[] playerRows;        // 4 rows, the first N are shown
        [SerializeField] TMP_Text[] playerRowNames;
        [SerializeField] Image[] playerRowPawns;         // tinted with the player's board colour
        [SerializeField] Image[] playerRowAvatars;       // the player's chosen picture

        [Header("Select Difficulty screen")]
        [SerializeField] Image[] difficultyCards;        // Easy, Medium, Hard
        [SerializeField] GameObject[] difficultyChecks;
        [SerializeField] Image[] opponentButtons;        // 1, 2, 3 opponents
        [SerializeField] Color selectedColor = new Color(0.86f, 1f, 0.88f);
        [SerializeField] Color normalColor = new Color(0.93f, 0.96f, 1f);

        int players = 2;
        int opponents = 1;
        AiDifficulty level = AiDifficulty.Medium;

        void Start()
        {
            RefreshDifficulty();
            RefreshPlayerRows();
        }

        // a name or photo can change from outside (Google / Facebook login, the online profile): keep the rows in step
        void OnEnable()
        {
            GameSettings.Changed += RefreshPlayerRows;
            ModePicker.Changed += RefreshForMode;          // Team Up fixes the seat count: keep the chips honest
        }

        void OnDisable()
        {
            GameSettings.Changed -= RefreshPlayerRows;
            ModePicker.Changed -= RefreshForMode;
        }

        void RefreshForMode()
        {
            if (GameSession.NeedsFourPlayers(ModePicker.Current)) { players = 4; opponents = 3; }
            RefreshPlayerRows();
            RefreshDifficulty();
        }

        // ---------- pass-and-play ----------

        public void ChooseLocal(int count)
        {
            players = GameSession.NeedsFourPlayers(ModePicker.Current) ? 4 : count;   // Team Up is 2 vs 2
            RefreshPlayerRows();
            router.Show(playerSelectScreen);
        }

        /// <summary>Opens the profile pop-up (name + picture) for one player. Used by the Edit buttons and the main-menu profile tag.</summary>
        public void EditProfile(int player)
        {
            profileEditor.Open(player, GameSettings.PlayerName(player), GameSettings.AvatarIndex(player), (newName, newAvatar) =>
            {
                GameSettings.SetPlayerName(player, newName);
                GameSettings.SetAvatarIndex(player, newAvatar);
                RefreshPlayerRows();
            });
        }

        public void StartLocalGame()
        {
            GameSession.Mode = ModePicker.Current;                 // the seat plan below depends on the mode
            GameSession.ConfigureLocal(GameSession.NeedsFourPlayers(GameSession.Mode) ? 4 : players);
            SceneLoader.Load(SceneLoader.Game);
        }

        void RefreshPlayerRows()
        {
            int[] seats = Board.DefaultSeats(players);      // 2 players sit opposite: Red and Yellow
            for (int i = 0; i < playerRows.Length; i++)
            {
                playerRows[i].SetActive(i < players);
                playerRowNames[i].text = GameSettings.PlayerName(i);
                playerRowAvatars[i].sprite = GameSettings.Picture(i);
                if (i < players) playerRowPawns[i].color = SeatStyle.Colors[seats[i]];
            }
        }

        // ---------- against the computer ----------

        public void ChooseVsComputer() => router.Show(difficultyScreen);

        /// <summary>
        /// Open one of the online screens (Friends, Leaderboards, Profile) from the main menu's bottom bar. Every one of
        /// them needs an account, so a player who has not chosen a login is sent to the login page first - the same gate
        /// Play Online uses, rather than a second one that could drift out of step with it.
        /// </summary>
        public void OpenOnlineScreen(int screen)
        {
            if (!LoginGate.HasChosen())
            {
                LoginGate.Purpose = LoginPurpose.Online;
                router.Show(welcomeScreen);
                return;
            }
            router.Show(screen);
        }

        /// <summary>
        /// Select Mode's "1 vs 1" and "4 Player" rows: remember the table size the player asked for and open Play Online,
        /// which is the screen that actually runs matchmaking. The size is stored where the online menu already reads it,
        /// so the two screens can never disagree about it.
        /// </summary>
        public void PlayOnlineAtSize(int players)
        {
            PlayerPrefs.SetInt("ludo.online.size", Mathf.Clamp(players, 2, 4));
            PlayerPrefs.Save();
            OpenOnlineScreen(onlineScreen);
        }

        /// <summary>Select Mode's "Tournaments" row: the Weekly Cup, which lives on the leaderboards screen's second tab.</summary>
        public void OpenWeeklyCup(int screen)
        {
            WeeklyCupTab = 1;
            OpenOnlineScreen(screen);
        }

        /// <summary>
        /// Which tab the leaderboards screen should open on (1 = Weekly Cup). The online code copies it into
        /// LeaderboardScreen.PendingTab; this side cannot see that class, so it leaves the number here instead.
        /// </summary>
        public static int WeeklyCupTab = -1;

        public void SetDifficulty(int index)
        {
            level = (AiDifficulty)index;
            RefreshDifficulty();
        }

        public void SetOpponents(int count)
        {
            opponents = GameSession.NeedsFourPlayers(ModePicker.Current) ? 3 : Mathf.Clamp(count, 1, 3);
            RefreshDifficulty();
        }

        public void StartComputerGame()
        {
            GameSession.Mode = ModePicker.Current;                 // Team Up seats a computer partner next to the player
            GameSession.ConfigureVsAi(GameSession.NeedsFourPlayers(GameSession.Mode) ? 3 : opponents, level);
            SceneLoader.Load(SceneLoader.Game);
        }

        void RefreshDifficulty()
        {
            for (int i = 0; i < difficultyCards.Length; i++)
            {
                bool on = i == (int)level;
                difficultyCards[i].color = on ? selectedColor : normalColor;
                difficultyChecks[i].SetActive(on);
            }
            // Team Up is 2 vs 2, so the count is fixed at three computer players and the other chips are shown greyed out
            int shown = GameSession.NeedsFourPlayers(ModePicker.Current) ? 3 : opponents;
            bool locked = GameSession.NeedsFourPlayers(ModePicker.Current);
            for (int i = 0; i < opponentButtons.Length; i++)
                opponentButtons[i].color = i + 1 == shown ? selectedColor : locked ? Color.Lerp(normalColor, Color.grey, 0.35f) : normalColor;
        }
    }
}
