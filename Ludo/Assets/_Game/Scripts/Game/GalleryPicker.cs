using System;
using System.IO;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// "Choose a picture from the gallery". On Android it opens the phone's own picture chooser (no storage or photo
    /// permission needed: the game only ever receives the one picture the player taps). In the Unity Editor it opens a
    /// file dialog so the feature can be tried on a PC. The result arrives as a small JPEG (bytes), or null when the
    /// player cancelled or it did not work.
    /// </summary>
    public sealed class GalleryPicker : MonoBehaviour
    {
        const string ObjectName = "LudoGalleryPicker";
        const int Size = 256;                    // the native side hands over a 256 x 256 square

        static GalleryPicker instance;
        Action<byte[]> callback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static bool Supported =>
#if UNITY_EDITOR
            true;
#else
            Application.platform == RuntimePlatform.Android;
#endif

        /// <summary>Open the chooser. The callback gets the picture (JPEG bytes) or null. Only one chooser at a time.</summary>
        public static void Pick(Action<byte[]> done)
        {
            if (instance == null)
            {
                var go = new GameObject(ObjectName);
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GalleryPicker>();
            }
            if (instance.callback != null) return;
            instance.callback = done;

#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Choose a profile picture", "", "png,jpg,jpeg");
            instance.Deliver(string.IsNullOrEmpty(path) ? "cancel" : path);
#elif UNITY_ANDROID
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var picker = new AndroidJavaClass("com.devorbit.ludofight.LudoGalleryPicker"))
                    picker.CallStatic("pick", activity, ObjectName, nameof(OnPicked), Size);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Gallery: " + e.Message);
                instance.Deliver("error:" + e.Message);
            }
#else
            instance.Deliver("error:not supported");
#endif
        }

        /// <summary>Called by the Android plugin (UnitySendMessage) with a file path, "cancel" or "error:...".</summary>
        void OnPicked(string result) => Deliver(result);

        void Deliver(string result)
        {
            var done = callback;
            callback = null;
            byte[] bytes = null;
            try
            {
                if (!string.IsNullOrEmpty(result) && result != "cancel" && !result.StartsWith("error:") && File.Exists(result))
                    bytes = File.ReadAllBytes(result);
                else if (!string.IsNullOrEmpty(result) && result.StartsWith("error:"))
                    Debug.LogWarning("[Ludo] Gallery: " + result);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Gallery: " + e.Message);
            }
            done?.Invoke(bytes);
        }
    }
}
