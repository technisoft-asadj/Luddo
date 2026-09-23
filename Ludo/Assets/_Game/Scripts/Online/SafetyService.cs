using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ludo.Online
{
    /// <summary>
    /// Player safety for public play (Google Play requires reporting and blocking when strangers can talk): block a
    /// player (they are muted, unfriended, and remembered on this phone) and report a player (opens an email to the
    /// developer with what happened). Nothing here throws.
    /// </summary>
    public static class SafetyService
    {
        const string BlockedKey = "ludo.blocked";
        static HashSet<string> blocked;

        static HashSet<string> Blocked
        {
            get
            {
                if (blocked == null)
                {
                    blocked = new HashSet<string>();
                    string saved = PlayerPrefs.GetString(BlockedKey, "");
                    foreach (var id in saved.Split(',')) if (id.Length > 0) blocked.Add(id);
                }
                return blocked;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => blocked = null;

        public static bool IsBlocked(string playerId) => !string.IsNullOrEmpty(playerId) && Blocked.Contains(playerId);

        public static int BlockedCount => Blocked.Count;

        public static void Block(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == OnlineService.PlayerId) return;
            if (Blocked.Add(playerId)) PlayerPrefs.SetString(BlockedKey, string.Join(",", Blocked));
            PlayerPrefs.Save();
            if (!VoiceService.IsMutedByMe(playerId)) VoiceService.ToggleMuteFor(playerId);   // stop hearing them right now
            _ = SocialService.BlockAsync(playerId);                                          // and never see a friend request from them
        }

        public static void Unblock(string playerId)
        {
            if (Blocked.Remove(playerId))
            {
                PlayerPrefs.SetString(BlockedKey, string.Join(",", Blocked));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Open an email to the developer about a player. Returns false if no support address is configured.</summary>
        public static bool Report(string playerId, string playerName, string reason)
        {
            var config = OnlineConfig.Load();
            string to = config != null ? config.supportEmail : "";
            if (string.IsNullOrEmpty(to)) return false;
            string subject = "Ludo Fight report: " + playerName;
            string body = "Reason: " + reason + "\nPlayer name: " + playerName + "\nPlayer id: " + playerId +
                          "\nRoom: " + RoomService.Code + "\nMy id: " + OnlineService.PlayerId +
                          "\nTime (UTC): " + DateTime.UtcNow.ToString("u");
            Application.OpenURL("mailto:" + to + "?subject=" + Uri.EscapeDataString(subject) + "&body=" + Uri.EscapeDataString(body));
            return true;
        }
    }
}
