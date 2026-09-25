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

        public static string ModeName(Ludo.Core.GameMode mode)
        {
            switch (mode)
            {
                case Ludo.Core.GameMode.Master: return "Master";
                case Ludo.Core.GameMode.Arrow: return "Arrow";
                case Ludo.Core.GameMode.Blitz: return "Blitz";
                case Ludo.Core.GameMode.TeamUp: return "Team Up";
                default: return "Classic";
            }
        }

        /// <summary>Team Up is always a four-seat table; the other modes take 2, 3 or 4.</summary>
        public static bool NeedsFourPlayers(Ludo.Core.GameMode mode) => mode == Ludo.Core.GameMode.TeamUp;

        /// <summary>One line that explains a mode (menus, room, game screen).</summary>
        public static string ModeRule(Ludo.Core.GameMode mode)
        {
            switch (mode)
            {
                case Ludo.Core.GameMode.Master: return "Capture an opponent before your pawns can go home.";
                case Ludo.Core.GameMode.Arrow: return "Stop on an arrow to slide 6 cells ahead.";
                case Ludo.Core.GameMode.Blitz: return "Pawns start out. First pawn home wins!";
                case Ludo.Core.GameMode.TeamUp: return "2 vs 2. Red+Yellow against Green+Blue. Both partners must finish.";
                default: return "Standard Ludo rules. All 4 pawns home wins.";
            }
        }

        /// <summary>Team Up: the two seats facing each other are partners. Elsewhere every seat plays for itself.</summary>
        public static bool ArePartners(int seatA, int seatB) =>
            Mode == Ludo.Core.GameMode.TeamUp && seatA % 2 == seatB % 2;

        /// <summary>Team Up: the name of a side, for the turn banner and the result screen.</summary>
        public static string TeamName(int seat) => seat % 2 == 0 ? "RED + YELLOW" : "GREEN + BLUE";

        /// <summary>
        /// How fast the match plays. Blitz is meant to be a sprint, so its pawn steps, pauses and thinking time run shorter;
        /// every other mode keeps the timing the rest of the game was tuned with. It never touches the online turn limit -
        /// people need the same time to think whatever the mode is.
        /// </summary>
        public static float Speed => Mode == Ludo.Core.GameMode.Blitz ? 1.6f : 1f;

        /// <summary>A pause or animation length in seconds, shortened for the fast modes (see Speed).</summary>
        public static float Beat(float seconds) => seconds / Speed;
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

        /// <summary>Sends a friend request to somebody met in this match, by online ID, and returns a message to show (set by the online code).</summary>
        public static System.Func<string, System.Threading.Tasks.Task<string>> AddFriend;

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
            AddFriend = null;
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

        /// <summary>
        /// Team Up: the badge says which side a seat is on instead of how it is played, because in a 2 vs 2 match knowing your
        /// partner matters more. 'mySeat' is the seat of the person holding this phone (-1 offline / pass and play).
        /// </summary>
        public static string SubtitleOf(PlayerSlot slot, int seat, int mySeat)
        {
            if (Mode != Ludo.Core.GameMode.TeamUp) return SubtitleOf(slot);
            if (mySeat >= 0)
            {
                if (seat == mySeat) return "You";
                return ArePartners(seat, mySeat) ? "Your partner" : "Opponent";
            }
            return TeamName(seat);
        }

        static int LocalPeople()
        {
            int n = 0;
            foreach (var s in Slots) if (!s.isAi && !s.isRemote) n++;
            return n;
        }
    }
}
