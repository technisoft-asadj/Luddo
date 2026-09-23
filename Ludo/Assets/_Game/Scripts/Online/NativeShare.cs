using UnityEngine;

namespace Ludo.Online
{
    /// <summary>Opens Android's share sheet with a piece of text (so the room code can go to WhatsApp, Messenger, SMS...).</summary>
    public static class NativeShare
    {
        public static void Text(string subject, string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.SUBJECT", subject);
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, subject))
                        activity.Call("startActivity", chooser);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Ludo] Share failed: " + e.Message);
                GUIUtility.systemCopyBuffer = text;
            }
#else
            GUIUtility.systemCopyBuffer = text;      // the Editor has no share sheet: copy instead
            Debug.Log("[Ludo] (Editor) share text copied: " + text);
#endif
        }
    }
}
