using UnityEngine;

namespace Ludo.Online
{
    /// <summary>
    /// "Share the game": opens the phone's share sheet (WhatsApp, Facebook, Messenger, SMS ...) with a short invitation and the
    /// game's store link, so a player can send Ludo Fight to a friend in two taps.
    /// </summary>
    public static class ShareApp
    {
        /// <summary>The Play Store page, used when OnlineConfig has no store link of its own.</summary>
        public const string DefaultStoreUrl = "https://play.google.com/store/apps/details?id=com.devorbit.ludofight";

        public static string StoreUrl
        {
            get
            {
                var config = OnlineConfig.Load();
                return config != null && !string.IsNullOrEmpty(config.storeUrl) ? config.storeUrl : DefaultStoreUrl;
            }
        }

        /// <summary>The message that goes out with the link.</summary>
        public static string Message => "Play Ludo Fight with me! Free Ludo - play online with friends or on one phone.\n" + StoreUrl;

        public static void Invite() => NativeShare.Text("Ludo Fight", Message);
    }

    /// <summary>A button in a scene can only point at a component, so this carries "Share the game" for the menu's share button.</summary>
    public sealed class ShareAppButton : MonoBehaviour
    {
        public void Share() => ShareApp.Invite();
    }
}
