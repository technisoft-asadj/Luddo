using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ludo.Online
{
    /// <summary>
    /// Results that are not in the cloud yet, kept on the phone (PlayerPrefs). A ranked match writes a provisional
    /// "disconnect loss" here the moment it starts and replaces it with the real result when the match ends properly. So a
    /// phone that loses its signal, or a player who closes the app to dodge a loss, still gets the loss the next time the
    /// stats load. Every entry has the match id, so one match is only ever counted once.
    /// </summary>
    public static class PendingOutcomes
    {
        const string Key = "ludo.pending.outcomes";

        [Serializable]
        sealed class Box { public List<MatchOutcome> items = new List<MatchOutcome>(); }

        static Box Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json)) return JsonUtility.FromJson<Box>(json) ?? new Box();
            }
            catch (Exception e) { Debug.LogWarning("[Ludo] Pending results unreadable: " + e.Message); }
            return new Box();
        }

        static void Save(Box box)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(box));
            PlayerPrefs.Save();
        }

        public static int Count => Load().items.Count;

        public static List<MatchOutcome> All() => Load().items;

        /// <summary>Add a result, or replace the one already waiting for the same match.</summary>
        public static void Set(MatchOutcome outcome)
        {
            var box = Load();
            int at = string.IsNullOrEmpty(outcome.matchId) ? -1 : box.items.FindIndex(o => o.matchId == outcome.matchId);
            if (at >= 0) box.items[at] = outcome; else box.items.Add(outcome);
            Save(box);
        }

        public static void Remove(string matchId)
        {
            var box = Load();
            box.items.RemoveAll(o => o.matchId == matchId);
            Save(box);
        }

        // ---------- ad bonus Rank Points not saved to the cloud yet ----------
        const string BonusKey = "ludo.pending.bonus";

        public static int Bonus => PlayerPrefs.GetInt(BonusKey, 0);

        public static void AddBonus(int points)
        {
            PlayerPrefs.SetInt(BonusKey, Bonus + points);
            PlayerPrefs.Save();
        }

        public static void ClearBonus(int points)
        {
            PlayerPrefs.SetInt(BonusKey, Math.Max(0, Bonus - points));
            PlayerPrefs.Save();
        }

        /// <summary>Drop every waiting result and bonus: they belong to the account that just left this phone.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BonusKey);
            PlayerPrefs.Save();
        }

        public static bool Has(string matchId) => Load().items.Exists(o => o.matchId == matchId);
    }
}
