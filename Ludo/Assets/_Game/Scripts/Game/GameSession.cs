using Ludo.AI;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>The colours and names of the four board seats, shared by the board, the HUD and the menus.</summary>
    public static class SeatStyle
    {
        public static readonly UnityEngine.Color[] Colors =
        {
            new UnityEngine.Color(0.91f, 0.17f, 0.22f),  // Red
            new UnityEngine.Color(0.12f, 0.66f, 0.33f),  // Green
            new UnityEngine.Color(1f, 0.74f, 0.05f),     // Yellow (golden, so it stays visible on white and pale yellow)
            new UnityEngine.Color(0.13f, 0.45f, 0.90f)   // Blue
        };
        public static readonly string[] Names = { "RED", "GREEN", "YELLOW", "BLUE" };
    }

    /// <summary>
    /// What the menus decided, waiting for the Game scene to read it. A static class is the simplest way to hand a
    /// few values from one scene to the next (a scene is destroyed when another one loads; statics survive).
    /// If the Game scene is opened directly in the Editor nothing is configured and the scene's own defaults are used.
    /// </summary>
    public static class GameSession
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsConfigured = false;
            Link = null;
            LeaveOnline = null;
            SpeakingProbe = null;
            Settler = null;
            ReturnToRoom = false;
            PlayerCount = 4;
            Mode = Ludo.Core.GameMode.Classic;
            Slots = new[] { PlayerSlot.Human, PlayerSlot.Human, PlayerSlot.Human, PlayerSlot.Human };
        }

        public static bool IsConfigured { get; private set; }

        /// <summary>The game mode of the next game (Classic / Master / Arrow / Blitz). Online it comes from the host's room.</summary>
        public static Ludo.Core.GameMode Mode { get; set; } = Ludo.Core.GameMode.Classic;

        public static string ModeName(Ludo.Core.GameMode mode) =>
            mode == Ludo.Core.GameMode.Master ? "Master" : mode == Ludo.Core.GameMode.Arrow ? "Arrow" : mode == Ludo.Core.GameMode.Blitz ? "Blitz" : "Classic";

        /// <summary>One line that explains a mode (menus, room, game screen).</summary>
        public static string ModeRule(Ludo.Core.GameMode mode)
        {
            switch (mode)
            {
                case Ludo.Core.GameMode.Master: return "Capture an opponent before your pawns can go home.";
                case Ludo.Core.GameMode.Arrow: return "Stop on an arrow to slide 6 cells ahead.";
                case Ludo.Core.GameMode.Blitz: return "Pawns start out. First pawn home wins!";
                default: return "Standard Ludo rules. All 4 pawns home wins.";
            }
        }
        public static int PlayerCount { get; private set; } = 4;
        public static PlayerSlot[] Slots { get; private set; } =
            { PlayerSlot.Human, PlayerSlot.Human, PlayerSlot.Human, PlayerSlot.Human };

        /// <summary>Pass-and-play: every seat is a person.</summary>
        public static void ConfigureLocal(int players)
        {
            Link = null;
            PlayerCount = players;
            Slots = new PlayerSlot[players];
            for (int i = 0; i < players; i++) Slots[i] = PlayerSlot.Human;
            IsConfigured = true;
        }

        /// <summary>One person against 1-3 computer players of the same difficulty.</summary>
        public static void ConfigureVsAi(int opponents, AiDifficulty level)
        {
            Link = null;
            PlayerCount = 1 + opponents;
            Slots = new PlayerSlot[PlayerCount];
            Slots[0] = PlayerSlot.Human;
            for (int i = 1; i < PlayerCount; i++) Slots[i] = PlayerSlot.Cpu(level);
            IsConfigured = true;
        }

        // ---------- online ----------

        /// <summary>The connection to the other phones, or null in an offline game.</summary>
        public static IMatchLink Link { get; private set; }
        public static bool IsOnline => Link != null;

        /// <summary>Called when the player leaves an online match (set by the online code: leaves the room).</summary>
        public static System.Action LeaveOnline;

        /// <summary>
        /// Settles an online match for this phone (records the result, works out Rank Points / XP / coins, handles the optional ad
        /// bonus) and gives the result screen a MatchSummary to show. Set by the online code, null offline.
        /// </summary>
        public static IMatchSettler Settler;

        /// <summary>Rematch: the menu should open straight on the waiting room (the room and the voice chat are still running).</summary>
        public static bool ReturnToRoom;

        /// <summary>Asks the voice chat "is the player with this online ID talking right now?" (set by the online code).</summary>
        public static System.Func<string, bool> SpeakingProbe;

        /// <summary>An online match: the seating plan came from the room; the phone that hosts the room rolls the dice.</summary>
        public static void ConfigureOnline(IMatchLink link, PlayerSlot[] seats, Ludo.Core.GameMode mode = Ludo.Core.GameMode.Classic)
        {
            Link = link;
            Mode = mode;
            PlayerCount = seats.Length;
            Slots = seats;
            IsConfigured = true;
        }

        /// <summary>Forget the online match (back in the menu).</summary>
        public static void ClearOnline()
        {
            Link = null;
            LeaveOnline = null;
            SpeakingProbe = null;
            Settler = null;
        }

        /// <summary>Text for the badge and the result screen.</summary>
        public static string NameOf(PlayerSlot slot, int index) =>
            slot.hasProfile ? slot.onlineName : slot.isAi ? "CPU " + index : GameSettings.PlayerName(index);

        /// <summary>The picture for the badge, turn banner and win screen: the player's chosen avatar, or the robot for the computer.</summary>
        public static Sprite AvatarOf(PlayerSlot slot, int index) =>
            slot.isAi ? AvatarLibrary.Computer
            : slot.hasProfile ? (ProfilePhoto.ForPlayer(slot.onlineId) ?? AvatarLibrary.Get(slot.onlineAvatar))
            : GameSettings.Picture(index);

        /// <summary>Online players only: the flag of the country they chose in their profile (null = none chosen, or an offline game).</summary>
        public static Sprite FlagOf(PlayerSlot slot) => slot.hasProfile ? FlagLibrary.Get(slot.onlineCountry) : null;

        public static string SubtitleOf(PlayerSlot slot) =>
            slot.isAi ? "Computer - " + slot.difficulty : slot.isRemote ? "Online" : LocalPeople() == 1 ? "You" : "Pass & Play";

        static int LocalPeople()
        {
            int n = 0;
            foreach (var s in Slots) if (!s.isAi && !s.isRemote) n++;
            return n;
        }
    }
}
