using System.Threading.Tasks;
using UnityEngine;

namespace Ludo.Online
{
    /// <summary>
    /// Step 1 of "Continue with Amazon": get the player's name/e-mail through the native Login with Amazon SDK
    /// (Assets/Plugins/Android/LudoAmazonLogin.androidlib). Step 2 (OnlineService.AmazonAsync) signs the player in.
    /// Android only, Amazon Appstore builds - the button is hidden on other platforms (see WelcomeScreen).
    /// </summary>
    public static class AmazonLogin
    {
        public static string LastError { get; private set; } = "";
        public static bool Supported => Application.platform == RuntimePlatform.Android;

        static TaskCompletionSource<(string name, string email, string userId)> pending;
        static AmazonLoginReceiver receiver;

        public static Task<(string name, string email, string userId)> LoginAsync()
        {
            if (!Supported)
            {
                LastError = "Amazon sign-in works on Android phones only.";
                return Task.FromResult<(string, string, string)>((null, null, null));
            }
            if (pending != null && !pending.Task.IsCompleted) return pending.Task;
            pending = new TaskCompletionSource<(string, string, string)>();
            EnsureReceiver();
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass("com.devorbit.ludofight.amazonlogin.AmazonLoginBridge");
            bridge.CallStatic("login", activity, receiver.name);
            return pending.Task;
        }

        public static void LogOut()
        {
            if (!Supported) return;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass("com.devorbit.ludofight.amazonlogin.AmazonLoginBridge");
                bridge.CallStatic("logout", activity);
            }
            catch (System.Exception e) { Debug.LogWarning("[Ludo] Amazon sign-out: " + e.Message); }
        }

        static void EnsureReceiver()
        {
            if (receiver != null) return;
            var go = new GameObject("AmazonLoginReceiver");
            Object.DontDestroyOnLoad(go);
            receiver = go.AddComponent<AmazonLoginReceiver>();
        }

        internal static void Resolve(string name, string email, string userId)
        {
            var t = pending; pending = null;
            LastError = "";
            t?.TrySetResult((name, email, userId));
        }

        internal static void Fail(string message)
        {
            var t = pending; pending = null;
            LastError = string.IsNullOrEmpty(message) ? "Amazon sign-in did not work. Try again." : message;
            t?.TrySetResult((null, null, null));
        }

        internal static void Cancel()
        {
            var t = pending; pending = null;
            LastError = "Amazon sign-in was cancelled.";
            t?.TrySetResult((null, null, null));
        }
    }

    /// <summary>The native plugin calls back into this (UnityPlayer.UnitySendMessage needs a real GameObject/component).</summary>
    public sealed class AmazonLoginReceiver : MonoBehaviour
    {
        public void OnAmazonSuccess(string payload)
        {
            string[] parts = payload.Split(new[] { "|||" }, System.StringSplitOptions.None);
            AmazonLogin.Resolve(parts.Length > 0 ? parts[0] : "", parts.Length > 1 ? parts[1] : "", parts.Length > 2 ? parts[2] : "");
        }

        public void OnAmazonError(string message) => AmazonLogin.Fail(message);
        public void OnAmazonCancel(string _) => AmazonLogin.Cancel();
    }
}
