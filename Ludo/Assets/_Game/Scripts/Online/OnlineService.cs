using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// The one door to Unity Gaming Services: starts the services and signs the player in. Everything online (rooms,
    /// friends, voice) first calls ConnectAsync(). It never throws: a failure becomes Status = Failed + LastError, so the
    /// menu can show a friendly message and the offline game keeps working.
    /// </summary>
    public static class OnlineService
    {
        public enum ConnectionStatus { Offline, Connecting, Ready, Failed }

        const string LoginMethodKey = "ludo.online.login";      // "", "guest" or "account" (what the player chose)
        const string AccountNameKey = "ludo.online.account";    // the username of a signed-in account (never the password)

        static Task<bool> pending;

        /// <summary>Test builds only (DevAutoRun -profile N): sign in under this named profile, so several clients on one computer are different players.</summary>
        public static string DevProfile = "";

        public static ConnectionStatus Status { get; private set; } = ConnectionStatus.Offline;
        public static string LastError { get; private set; } = "";
        public static event Action StatusChanged;

        public static bool IsReady => Status == ConnectionStatus.Ready;
        public static string PlayerId => IsReady ? AuthenticationService.Instance.PlayerId : "";
        public static string LoginMethod => PlayerPrefs.GetString(LoginMethodKey, "");

        /// <summary>The player has seen the login page and chosen guest or an account.</summary>
        public static bool HasChosenLogin => LoginMethod.Length > 0;
        /// <summary>Signed in with a real account (username, Google or Facebook) rather than as a guest.</summary>
        public static bool IsAccount => LoginMethod == "account" || LoginMethod == "google" || LoginMethod == "facebook";
        public static bool IsGuest => LoginMethod == "guest";
        public static string AccountName => PlayerPrefs.GetString(AccountNameKey, "");

        /// <summary>"Guest", "Google", "Facebook" or "Account" (username + password): how this player is signed in.</summary>
        public static string ProviderLabel =>
            LoginMethod == "google" ? "Google" : LoginMethod == "facebook" ? "Facebook" : LoginMethod == "account" ? "Account" : IsGuest ? "Guest" : "";

        const string UsedKey = "ludo.online.used";

        /// <summary>True once this phone has created an online account (so "delete account" has something to delete).</summary>
        public static bool HasAccount => PlayerPrefs.GetInt(UsedKey, 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LoginGate.HasChosen = () => HasChosenLogin;      // the splash screen asks this: a returning player (guest or account) goes straight to the menu
            pending = null;
            Status = ConnectionStatus.Offline;
            LastError = "";
            StatusChanged = null;
        }

        /// <summary>
        /// A player who already chose a login (guest or account) is connected quietly in the background as the app opens, so
        /// the coins, the chest and My Dice are ready on the home screen without opening Play Online first. A logged-out
        /// phone is left alone (ConnectAsync refuses it), and a failure here is silent: Play Online still shows the error.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void ConnectInBackground()
        {
            if (!HasChosenLogin) return;
            try
            {
                if (await ConnectAsync()) await StatsService.LoadMineAsync();
            }
            catch (System.Exception e) { Debug.LogWarning("[Ludo] Background connect failed: " + e.Message); }
        }

        /// <summary>Start the services and sign in (as a guest unless the player logged in before). Safe to call many times.</summary>
        public static Task<bool> ConnectAsync()
        {
            if (IsReady) return Task.FromResult(true);
            if (!HasChosenLogin) return Task.FromResult(Fail("Log in to play online."));   // a logged-out phone never signs in by itself
            if (pending != null && !pending.IsCompleted) return pending;
            pending = ConnectCore();
            return pending;
        }

        static async Task<bool> ConnectCore()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return Fail("No internet connection.");

            SetStatus(ConnectionStatus.Connecting);
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    if (!string.IsNullOrEmpty(DevProfile)) AuthenticationService.Instance.SwitchProfile(DevProfile);
                    if (IsAccount && !AuthenticationService.Instance.SessionTokenExists)
                    {
                        ClearLocalIdentity();                                        // the saved login is gone: a clean logged-out phone, log in again
                        return Fail("Please log in again.");
                    }
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();   // reuses the saved session (guest OR account) on later launches
                }

                LastError = "";
                PlayerPrefs.SetInt(UsedKey, 1);
                SetStatus(ConnectionStatus.Ready);
                OnSignedIn();
                return true;
            }
            catch (Exception e)
            {
                return Fail(Describe(e));
            }
        }

        /// <summary>
        /// Log out on this phone. Results still waiting on the phone are saved first (so logging out cannot dodge a loss), then
        /// everything that belonged to this player is forgotten. A guest who logs out loses that guest identity; a Google or
        /// Facebook account comes back with its progress the next time it logs in.
        /// </summary>
        public static async Task SignOutAsync()
        {
            if (IsReady) await StatsService.FlushPendingAsync();
            await RoomService.LeaveAsync();
            await VoiceService.LeaveAsync();
            ClearLocalIdentity();
        }

        /// <summary>
        /// A clean logged-out phone: the saved session, the Facebook login, the friends connection, cached stats and waiting
        /// results, and the name, avatar and photo the account put on the profile are all removed. The next check sees nobody.
        /// </summary>
        static void ClearLocalIdentity()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut(true);                    // true: the saved session token goes too
            SetStatus(ConnectionStatus.Offline);                                 // first, so the profile changes below sync nothing
            FacebookLogin.LogOut();
            SocialService.Stop();
            StatsService.ForgetMine();
            PendingOutcomes.Clear();
            PhotoService.ForgetUploaded();
            ProfilePhoto.MyOnlineId = "";
            if (ProfilePhoto.Has) ProfilePhoto.ClearMine();
            GameSettings.ResetPlayer(0);
            ForgetChoice();
            PlayerPrefs.DeleteKey(UsedKey);
            PlayerPrefs.Save();
        }

        static void ForgetChoice()
        {
            PlayerPrefs.DeleteKey(LoginMethodKey);
            PlayerPrefs.DeleteKey(AccountNameKey);
            PlayerPrefs.Save();
        }

        // ---------- login page: guest ----------

        /// <summary>
        /// "Continue as Guest" on the first screen. Needs no internet: it only remembers the choice, so the whole offline game
        /// works at once. The guest identity is created the first time the player opens Play Online.
        /// </summary>
        public static void ChooseGuest()
        {
            if (HasChosenLogin) return;
            PlayerPrefs.SetString(LoginMethodKey, "guest");
            PlayerPrefs.Save();
        }

        /// <summary>Connected: tell the photo code who I am and make sure my photo and name are in the cloud.</summary>
        static void OnSignedIn(bool syncPhoto = true)
        {
            ProfilePhoto.MyOnlineId = AuthenticationService.Instance.PlayerId;
            _ = PushProfileNameAsync();
            if (syncPhoto) _ = PhotoService.SyncMineAsync();
        }

        // ---------- Google and Facebook ----------

        /// <summary>Result of "Continue with Google / Facebook".</summary>
        public enum SocialResult
        {
            Done,           // signed in (a guest was upgraded and keeps everything)
            NeedsChoice,    // that Google/Facebook account already has a Ludo Fight profile: the player must agree to switch to it
            Failed          // see LastError
        }

        /// <summary>Log in with Google. A guest is upgraded in place (same player, same friends, same stats).</summary>
        public static async Task<SocialResult> GoogleAsync()
        {
            string code = await GoogleLogin.GetAuthCodeAsync(false);
            if (code == null) { LastError = GoogleLogin.LastError; return SocialResult.Failed; }
            return await SocialSignIn("google", code,
                c => AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(c),
                c => AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(c), "Google");
        }

        /// <summary>Log in with Facebook (needs the Facebook app to be set up, otherwise it says so).</summary>
        public static async Task<SocialResult> FacebookAsync()
        {
            string token = await FacebookLogin.GetAccessTokenAsync();
            if (token == null) { LastError = FacebookLogin.LastError; return SocialResult.Failed; }
            return await SocialSignIn("facebook", token,
                t => AuthenticationService.Instance.LinkWithFacebookAsync(t),
                t => AuthenticationService.Instance.SignInWithFacebookAsync(t), "Facebook");
        }

        /// <summary>The player agreed to leave the current (guest) profile and use the one that already belongs to their Google account.</summary>
        public static async Task<bool> SwitchToGoogleAsync()
        {
            string code = await GoogleLogin.GetAuthCodeAsync(true);       // a fresh code: the first one was used up by the link attempt
            if (code == null) { LastError = GoogleLogin.LastError; return false; }
            return await SwitchAccount("google", () => AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(code), "Google");
        }

        public static async Task<bool> SwitchToFacebookAsync()
        {
            string token = await FacebookLogin.GetAccessTokenAsync();
            if (token == null) { LastError = FacebookLogin.LastError; return false; }
            return await SwitchAccount("facebook", () => AuthenticationService.Instance.SignInWithFacebookAsync(token), "Facebook");
        }

        static async Task<SocialResult> SocialSignIn(string method, string credential, Func<string, Task> link, Func<string, Task> signIn, string name)
        {
            try
            {
                if (!await EnsureServices()) return SocialResult.Failed;
                bool upgrade = AuthenticationService.Instance.IsSignedIn;
                if (upgrade) await link(credential);      // upgrade the current guest
                else await signIn(credential);
                Chosen(method, name);
                if (upgrade) await ProviderProfile.ImportAsync(method);       // the guest keeps everything and takes the account's name and photo
                else await ProviderProfile.RestoreAsync(method);              // first screen / new phone: the saved profile, or the account's name and photo
                return SocialResult.Done;
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return SocialResult.NeedsChoice;
            }
            catch (Exception e)
            {
                LastError = SocialMessage(e, name);
                return SocialResult.Failed;
            }
        }

        static async Task<bool> SwitchAccount(string method, Func<Task> signIn, string name)
        {
            try
            {
                if (!await EnsureServices()) return false;
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(true);   // leave the guest behind
                await signIn();
                StatsService.ForgetMine();                       // the other profile has its own stats
                Chosen(method, name);
                await ProviderProfile.RestoreAsync(method);
                return true;
            }
            catch (Exception e)
            {
                LastError = SocialMessage(e, name);
                return false;
            }
        }

        static string SocialMessage(Exception e, string name)
        {
            string text = (e.Message ?? "").ToLowerInvariant();
            if (text.Contains("not available") || text.Contains("permission_denied") || text.Contains("not enabled") || text.Contains("provider"))
                return name + " login is not switched on for this game yet. You can still play as a guest.";
            if (e is RequestFailedException r && r.ErrorCode == CommonErrorCodes.TransportError) return "No connection. Check your internet and try again.";
            return Describe(e);
        }

        static async Task<bool> EnsureServices()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable) { LastError = "No internet connection."; return false; }
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                return true;
            }
            catch (Exception e)
            {
                LastError = Describe(e);
                return false;
            }
        }

        static void Chosen(string method, string username)
        {
            LastError = "";
            PlayerPrefs.SetString(LoginMethodKey, method);
            PlayerPrefs.SetString(AccountNameKey, username);       // for Google/Facebook this is just the provider name
            PlayerPrefs.SetInt(UsedKey, 1);
            PlayerPrefs.Save();
            SetStatus(ConnectionStatus.Ready);
            OnSignedIn(syncPhoto: false);         // the photo is synced once the profile has been restored/imported (ProviderProfile)
        }

        /// <summary>
        /// Permanently delete the online account (Google Play requires this inside the app): its public stats and photo, then
        /// the account itself, then everything this phone kept about it. Afterwards the phone is logged out.
        /// </summary>
        public static async Task<bool> DeleteAccountAsync()
        {
            try
            {
                if (!await ConnectAsync()) return false;
                await RoomService.LeaveAsync();
                await VoiceService.LeaveAsync();
                await StatsService.DeleteMineAsync();                    // erase the public data first, then the account itself
                await PhotoService.DeleteMineAsync();
                await AuthenticationService.Instance.DeleteAccountAsync();
                ClearLocalIdentity();
                return true;
            }
            catch (Exception e)
            {
                LastError = Describe(e);
                return false;
            }
        }

        /// <summary>Unity's rule for online names: no spaces, at most 50 characters.</summary>
        public static string CloudNameFrom(string displayName)
        {
            string n = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim().Replace(' ', '_');
            return n.Length > 50 ? n.Substring(0, 50) : n;
        }

        /// <summary>The name to show for someone else: Unity adds "#1234" to make names unique; players do not need to see it.</summary>
        public static string ShownName(string cloudName)
        {
            if (string.IsNullOrEmpty(cloudName)) return "Player";
            int hash = cloudName.LastIndexOf('#');
            string n = hash > 0 ? cloudName.Substring(0, hash) : cloudName;
            return n.Replace('_', ' ');
        }

        /// <summary>Copy the profile name to the cloud so friends see it. Best effort: a failure is not an error for the player.</summary>
        public static async Task PushProfileNameAsync()
        {
            try
            {
                if (!IsReady) return;
                string wanted = CloudNameFrom(GameSettings.PlayerName(0));
                string current = AuthenticationService.Instance.PlayerName;
                if (string.IsNullOrEmpty(current) || ShownName(current) != wanted.Replace('_', ' '))
                    await AuthenticationService.Instance.UpdatePlayerNameAsync(wanted);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Could not update the online name: " + e.Message);
            }
        }

        static bool Fail(string message)
        {
            LastError = message;
            Debug.LogWarning("[Ludo] Online: " + message);
            SetStatus(ConnectionStatus.Failed);
            return false;
        }

        static void SetStatus(ConnectionStatus s)
        {
            Status = s;
            StatusChanged?.Invoke();
        }

        /// <summary>Turn an exception into one short sentence a player can read.</summary>
        public static string Describe(Exception e)
        {
            if (e is AuthenticationException a)
                return "Sign-in failed (" + a.ErrorCode + "): " + a.Message;
            if (e is RequestFailedException r)
                return "Online service error (" + r.ErrorCode + "): " + r.Message;
            return e.Message;
        }
    }
}
