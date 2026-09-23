using System;
using Ludo.AI;

namespace Ludo.Game
{
    /// <summary>Who controls one seat: a person tapping this screen, the computer, or (online) a person on another phone.
    /// The menus fill these in; opened alone in the Editor the Inspector values are used.</summary>
    [Serializable]
    public struct PlayerSlot
    {
        public bool isAi;
        public AiDifficulty difficulty;

        // ---- online games only ----
        public bool isRemote;          // a person on another phone (this phone waits for their moves)
        public bool hasProfile;        // name and picture below come from the online room
        public string onlineName;
        public int onlineAvatar;
        public string onlineId;        // the player's online ID (to know who is talking in voice chat)
        public string onlineCountry;   // ISO code of the country the player chose ("" = none): shown as a flag

        public static PlayerSlot Human => new PlayerSlot();
        public static PlayerSlot Cpu(AiDifficulty level) => new PlayerSlot { isAi = true, difficulty = level };

        public static PlayerSlot Online(string playerName, int avatar, bool remote, string playerId = "", string country = "") =>
            new PlayerSlot { isRemote = remote, hasProfile = true, onlineName = playerName, onlineAvatar = avatar, onlineId = playerId, onlineCountry = country ?? "" };

        public static PlayerSlot OnlineCpu(string playerName, AiDifficulty level) =>
            new PlayerSlot { isAi = true, difficulty = level, hasProfile = true, onlineName = playerName, onlineId = "", onlineCountry = "" };
    }
}
