using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Fills in the player's profile after they log in: the name and picture of their Google / Facebook account, or the
    /// profile already saved in the cloud when they come back on another phone. The player can change both afterwards.
    /// Best effort: if anything fails the player simply keeps the name and avatar they already had.
    /// </summary>
    public static class ProviderProfile
    {
        const int MaxNameLength = 14;

        /// <summary>The Google / Facebook name and picture become my profile (a guest who just linked an account).</summary>
        public static async Task ImportAsync(string method)
        {
            try
            {
                string name = null, pictureUrl = null;
                if (method == "google") { name = GoogleLogin.DisplayName; pictureUrl = GoogleLogin.ImageUrl; }
                else if (method == "facebook")
                {
                    var facebook = await FacebookLogin.GetProfileAsync();
                    name = facebook.name; pictureUrl = facebook.pictureUrl;
                }
                string shown = ShortName(name);
                if (shown.Length > 0) GameSettings.SetPlayerName(0, shown);
                if (!string.IsNullOrEmpty(pictureUrl))
                {
                    var bytes = await DownloadAsync(pictureUrl);
                    if (bytes != null) ProfilePhoto.SetMine(bytes);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Importing the " + method + " profile: " + e.Message);
            }
            await Finish();
        }

        /// <summary>The profile is settled: publish the name and photo so other players see them.</summary>
        static async Task Finish()
        {
            await OnlineService.PushProfileNameAsync();
            await PhotoService.SyncMineAsync();
        }

        /// <summary>
        /// Logged in to an account that may already have a saved profile (new phone, or switching from a guest): take its name,
        /// avatar and photo from the cloud. A brand-new account gets the Google / Facebook name and photo instead.
        /// </summary>
        public static async Task RestoreAsync(string method)
        {
            try
            {
                PhotoService.ForgetUploaded();
                var saved = await StatsService.LoadOfAsync(OnlineService.PlayerId);
                if (saved != null && !string.IsNullOrWhiteSpace(saved.name))
                {
                    GameSettings.SetPlayerName(0, saved.name);
                    GameSettings.SetAvatarIndex(0, saved.avatar);
                    GameSettings.Country = saved.country;             // that account's own country ("" if it never chose one)
                    if (!await PhotoService.PullMineAsync() && ProfilePhoto.Has) ProfilePhoto.ClearMine();     // the other phone's photo is not mine
                    StatsService.ForgetMine();
                    await Finish();
                    return;
                }
                if (ProfilePhoto.Has) ProfilePhoto.ClearMine();
                GameSettings.Country = "";                           // a new account chooses its own country (never the previous player's)
                if (method == "google" || method == "facebook") await ImportAsync(method);      // (finishes by itself)
                else await Finish();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Restoring the profile: " + e.Message);
            }
        }

        /// <summary>"Mohsin Sultan Harrie" -> "Mohsin"; long single names are cut. Empty when there is nothing usable.</summary>
        public static string ShortName(string full)
        {
            string name = (full ?? "").Trim();
            if (name.Length <= MaxNameLength) return name;
            int space = name.IndexOf(' ');
            if (space > 0) name = name.Substring(0, space);
            return name.Length > MaxNameLength ? name.Substring(0, MaxNameLength) : name;
        }

        static async Task<byte[]> DownloadAsync(string url)
        {
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return null;
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 12;
                var done = new TaskCompletionSource<bool>();
                request.SendWebRequest().completed += _ => done.TrySetResult(true);
                await done.Task;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Ludo] Profile picture download: " + request.error);
                    return null;
                }
                return request.downloadHandler.data;
            }
        }
    }
}
