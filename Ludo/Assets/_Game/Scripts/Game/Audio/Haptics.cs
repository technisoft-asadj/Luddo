using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// Short phone vibrations (capture, reaching home, winning). Respects the Vibration switch in Settings.
    /// Unity's own Handheld.Vibrate() can only buzz for a fixed ~1 second, so short pulses call Android's Vibrator directly.
    /// Does nothing in the Editor and on non-Android platforms.
    /// </summary>
    public static class Haptics
    {
        /// <summary>A short tap-like pulse.</summary>
        public static void Pulse(int milliseconds)
        {
            if (!GameSettings.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(milliseconds);
#endif
        }

        /// <summary>A long celebratory buzz for winning. Also makes Unity add the VIBRATE permission to the app.</summary>
        public static void Buzz()
        {
            if (!GameSettings.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk = -1;

        static void VibrateAndroid(int milliseconds)
        {
            try
            {
                if (vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        sdk = version.GetStatic<int>("SDK_INT");
                }
                if (vibrator == null) return;

                if (sdk >= 26)
                {
                    // VibrationEffect exists from Android 8; -1 = the device's default strength
                    using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, -1))
                        vibrator.Call("vibrate", oneShot);
                }
                else
                {
                    vibrator.Call("vibrate", (long)milliseconds);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Ludo] Vibration failed: " + e.Message);
            }
        }
#endif
    }
}
