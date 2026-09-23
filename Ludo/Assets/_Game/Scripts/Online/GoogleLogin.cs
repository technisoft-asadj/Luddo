using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace Ludo.Online
{
    /// <summary>
    /// Step 1 of "Log in with Google": ask the Google Play Games app on the phone who the player is and get a one-time
    /// code for our Web client. Step 2 (in OnlineService) gives that code to Unity Authentication, which checks it with
    /// Google and returns the Ludo Fight player. Works on Android phones only (there is nothing to ask on a PC).
    /// Every failure becomes a short sentence in LastError; nothing throws.
    /// </summary>
    public static class GoogleLogin
    {
        public static string LastError { get; private set; } = "";

        public static bool Supported => Application.platform == RuntimePlatform.Android;

        /// <summary>The name and picture address of the signed-in Google Play Games profile (empty until signed in).</summary>
        public static string DisplayName
        {
            get
            {
#if UNITY_ANDROID
                try { return Supported && PlayGamesPlatform.Instance.IsAuthenticated() ? PlayGamesPlatform.Instance.GetUserDisplayName() ?? "" : ""; }
                catch (System.Exception) { return ""; }
#else
                return "";
#endif
            }
        }

        public static string ImageUrl
        {
            get
            {
#if UNITY_ANDROID
                try { return Supported && PlayGamesPlatform.Instance.IsAuthenticated() ? PlayGamesPlatform.Instance.GetUserImageUrl() ?? "" : ""; }
                catch (System.Exception) { return ""; }
#else
                return "";
#endif
            }
        }

        /// <summary>The one-time code for Unity Authentication, or null (see LastError). Ask again for every attempt: a code works once.</summary>
        public static Task<string> GetAuthCodeAsync(bool forceRefresh)
        {
            LastError = "";
#if UNITY_ANDROID
            if (!Supported) { LastError = "Google login works on Android phones only."; return Task.FromResult<string>(null); }
            var done = new TaskCompletionSource<string>();
            try
            {
                var platform = PlayGamesPlatform.Instance;
                platform.Authenticate(status =>
                {
                    if (status == SignInStatus.Success) { RequestCode(platform, forceRefresh, done); return; }
                    // not signed in to Google Play Games yet: show Google's own sign-in screen
                    platform.ManuallyAuthenticate(second =>
                    {
                        if (second == SignInStatus.Success) RequestCode(platform, forceRefresh, done);
                        else Fail(done, second == SignInStatus.Canceled ? "Google sign-in was cancelled." : "Google sign-in did not work (" + second + "). Check that Google Play Games is up to date.");
                    });
                });
            }
            catch (System.Exception e)
            {
                Fail(done, "Google sign-in failed: " + e.Message);
            }
            return done.Task;
#else
            LastError = "Google login works on Android phones only.";
            return Task.FromResult<string>(null);
#endif
        }

#if UNITY_ANDROID
        static void RequestCode(PlayGamesPlatform platform, bool forceRefresh, TaskCompletionSource<string> done)
        {
            platform.RequestServerSideAccess(forceRefresh, code =>
            {
                if (string.IsNullOrEmpty(code)) Fail(done, "Google did not give the game a sign-in code. Try again.");
                else done.TrySetResult(code);
            });
        }

        static void Fail(TaskCompletionSource<string> done, string message)
        {
            LastError = message;
            Debug.LogWarning("[Ludo] " + message);
            done.TrySetResult(null);
        }
#endif
    }
}
