using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using UnityEngine;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Shares profile photos. My photo (128 x 128 JPEG, about 5 KB) is kept in my public Cloud Save data next to my stats,
    /// so other players in a room can fetch it. The room only carries a tiny "photo stamp" (a checksum) so nobody
    /// downloads a picture that has not changed. Everything here is best effort: no photo just means the animal avatar.
    /// </summary>
    public static class PhotoService
    {
        const string Key = "photo";
        const string UploadedKey = "ludo.photo.uploaded";       // the stamp of the photo last uploaded from this phone
        const int MaxTries = 3;

        static readonly Dictionary<string, string> cachedStamp = new Dictionary<string, string>();
        static readonly Dictionary<string, int> tries = new Dictionary<string, int>();
        static readonly HashSet<string> loading = new HashSet<string>();

        /// <summary>A photo of some player arrived (the room and menus redraw).</summary>
        public static event Action<string> Loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cachedStamp.Clear();
            tries.Clear();
            loading.Clear();
            Loaded = null;
        }

        /// <summary>"0" when I have no photo, otherwise a short checksum that changes whenever the photo changes.</summary>
        public static string MineStamp()
        {
            var bytes = ProfilePhoto.MineBytes();
            return bytes == null ? "0" : Stamp(bytes);
        }

        public static string Stamp(byte[] bytes)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (byte b in bytes) hash = (hash ^ b) * 16777619;
                return hash.ToString("x") + bytes.Length.ToString("x");
            }
        }

        // ---------- my photo -> the cloud ----------

        /// <summary>Make the cloud copy of my photo match the phone: upload it, or delete it when I removed it.</summary>
        public static async Task SyncMineAsync()
        {
            try
            {
                if (!OnlineService.IsReady) return;          // never signs in by itself (a logged-out phone must stay logged out)
                string stamp = MineStamp();
                if (PlayerPrefs.GetString(UploadedKey, "0") == stamp) return;       // already up to date
                if (stamp == "0")
                {
                    try { await CloudSaveService.Instance.Data.Player.DeleteAsync(Key, new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions(new PublicWriteAccessClassOptions())); }
                    catch (Exception) { /* nothing was there */ }
                }
                else
                {
                    var bytes = ProfilePhoto.MineBytes();
                    if (bytes == null) return;
                    await CloudSaveService.Instance.Data.Player.SaveAsync(
                        new Dictionary<string, object> { { Key, Convert.ToBase64String(bytes) } },
                        new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new PublicWriteAccessClassOptions()));
                }
                PlayerPrefs.SetString(UploadedKey, stamp);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Photo upload: " + e.Message);
            }
        }

        /// <summary>Remove my photo from the cloud (part of deleting the online account). Best effort.</summary>
        public static async Task DeleteMineAsync()
        {
            try
            {
                if (!OnlineService.IsReady) return;
                await CloudSaveService.Instance.Data.Player.DeleteAsync(Key, new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions(new PublicWriteAccessClassOptions()));
            }
            catch (Exception) { /* there was no photo */ }
        }

        /// <summary>Forget what this phone uploaded (another account signed in): the next sync uploads for the new owner.</summary>
        public static void ForgetUploaded()
        {
            PlayerPrefs.DeleteKey(UploadedKey);
        }

        /// <summary>Somebody's photo as JPEG bytes from the cloud, or null when they have none.</summary>
        public static async Task<byte[]> DownloadAsync(string playerId)
        {
            try
            {
                if (!await OnlineService.ConnectAsync()) return null;
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { Key }, new LoadOptions(new PublicReadAccessClassOptions(playerId)));
                if (!items.TryGetValue(Key, out var item)) return null;
                string text = item.Value.GetAs<string>();
                return string.IsNullOrEmpty(text) ? null : Convert.FromBase64String(text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Photo download: " + e.Message);
                return null;
            }
        }

        /// <summary>I signed in on a phone that has another photo (or none): take mine from the cloud. True when a photo was found.</summary>
        public static async Task<bool> PullMineAsync()
        {
            var bytes = await DownloadAsync(OnlineService.PlayerId);
            if (bytes != null && ProfilePhoto.SetMine(bytes))
            {
                PlayerPrefs.SetString(UploadedKey, MineStamp());       // it came from the cloud: nothing to upload
                return true;
            }
            return false;
        }

        // ---------- other players' photos ----------

        /// <summary>
        /// Make sure the photo of this player (with this stamp, see MineStamp) is known. Cheap to call again and again from a
        /// redraw: it downloads only when the stamp is new and never more than a few times for one player.
        /// </summary>
        public static async void Ensure(string playerId, string stamp)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == OnlineService.PlayerId) return;
            if (string.IsNullOrEmpty(stamp) || stamp == "0")
            {
                if (cachedStamp.Remove(playerId)) ProfilePhoto.ForgetOther(playerId);       // they removed their photo
                return;
            }
            if (cachedStamp.TryGetValue(playerId, out var known) && known == stamp) return;
            if (loading.Contains(playerId)) return;
            tries.TryGetValue(playerId + stamp, out int used);
            if (used >= MaxTries) return;
            tries[playerId + stamp] = used + 1;

            loading.Add(playerId);
            var bytes = await DownloadAsync(playerId);
            loading.Remove(playerId);
            if (bytes != null && ProfilePhoto.SetOther(playerId, bytes))
            {
                cachedStamp[playerId] = stamp;
                Debug.Log("[Ludo] Photo of " + playerId + " loaded (" + bytes.Length + " bytes)");
                Loaded?.Invoke(playerId);
            }
        }
    }
}
