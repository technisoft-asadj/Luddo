using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if LUDO_FACEBOOK
using Facebook.Unity;
#endif

namespace Ludo.Online
{
    /// <summary>
    /// Step 1 of "Log in with Facebook": get a Facebook access token through Facebook's SDK. Step 2 (OnlineService) gives
    /// it to Unity Authentication. The Facebook SDK and the Facebook app ID are added only when the game owner has created
    /// the Facebook app (see RELEASE_GUIDE.md); until then the scripting symbol LUDO_FACEBOOK is off, this class reports
    /// "not set up yet" and the game builds without any Facebook code.
    /// </summary>
    public static class FacebookLogin
    {
        public static string LastError { get; private set; } = "";

        /// <summary>
        /// Is Facebook login part of this version? Off for version 1.0 (the owner decided to ship without it; the code and SDK stay
        /// for the next version). Switch on = add the scripting symbol LUDO_FACEBOOK and rebuild the scenes.
        /// </summary>
#if LUDO_FACEBOOK
        public const bool Enabled = true;
#else
        public const bool Enabled = false;
#endif

#if LUDO_FACEBOOK
        public static bool Supported => Application.platform == RuntimePlatform.Android;

        public static async Task<string> GetAccessTokenAsync()
        {
            LastError = "";
            if (!Supported) { LastError = "Facebook login works on Android phones only."; return null; }
            try
            {
                if (!FB.IsInitialized)
                {
                    var ready = new TaskCompletionSource<bool>();
                    FB.Init(() => ready.TrySetResult(FB.IsInitialized));
                    if (!await ready.Task) { LastError = "Facebook could not start."; return null; }
                }
                FB.ActivateApp();
                if (!FB.IsLoggedIn)
                {
                    var login = new TaskCompletionSource<ILoginResult>();
                    FB.LogInWithReadPermissions(new List<string> { "public_profile" }, r => login.TrySetResult(r));
                    var result = await login.Task;
                    if (result == null || result.Error != null || result.Cancelled || !FB.IsLoggedIn)
                    {
                        LastError = result != null && result.Cancelled ? "Facebook login was cancelled." : "Facebook login did not work. Try again.";
                        return null;
                    }
                }
                return AccessToken.CurrentAccessToken.TokenString;
            }
            catch (System.Exception e)
            {
                LastError = "Facebook login failed: " + e.Message;
                return null;
            }
        }

        public static void LogOut()
        {
            if (FB.IsInitialized && FB.IsLoggedIn) FB.LogOut();
        }

        /// <summary>The logged-in person's name and profile picture address.</summary>
        public static async Task<(string name, string pictureUrl)> GetProfileAsync()
        {
            try
            {
                if (!FB.IsInitialized || !FB.IsLoggedIn) return ("", "");
                var reply = new TaskCompletionSource<IGraphResult>();
                FB.API("/me?fields=name,picture.width(200).height(200)", HttpMethod.GET, r => reply.TrySetResult(r));
                var result = await reply.Task;
                if (result == null || result.Error != null) return ("", "");
                string name = result.ResultDictionary.TryGetValue("name", out var n) ? n as string : "";
                string url = "";
                if (result.ResultDictionary.TryGetValue("picture", out var p) && p is System.Collections.Generic.IDictionary<string, object> pic
                    && pic.TryGetValue("data", out var d) && d is System.Collections.Generic.IDictionary<string, object> data
                    && data.TryGetValue("url", out var u)) url = u as string;
                return (name ?? "", url ?? "");
            }
            catch (System.Exception) { return ("", ""); }
        }
#else
        public static bool Supported => false;

        public static Task<string> GetAccessTokenAsync()
        {
            LastError = "Facebook login is not set up in this version yet.";
            return Task.FromResult<string>(null);
        }

        public static void LogOut() { }

        public static Task<(string name, string pictureUrl)> GetProfileAsync() => Task.FromResult(("", ""));
#endif
    }
}
