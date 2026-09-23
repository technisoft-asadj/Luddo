using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// The online part of the Settings screen: read the privacy policy inside the app, and delete the online account
    /// (Google Play requires both for apps with accounts). Offline settings and names are not touched by deleting.
    /// </summary>
    public sealed class OnlineSettings : MonoBehaviour
    {
        [SerializeField] GameObject policyModal;
        [SerializeField] Button openOnlineButton;         // "Open full policy" (only when a web address is set)
        [SerializeField] TMP_Text policyBody;
        [SerializeField] GameObject deleteModal;
        [SerializeField] TMP_Text deleteMessage;
        [SerializeField] Button deleteConfirmButton;
        [SerializeField] TMP_Text deleteConfirmLabel;
        [SerializeField] GameObject deleteAccountButton;  // only shown when there is a signed-in online account to delete

        [SerializeField] ScreenRouter router;
        [SerializeField] int welcomeScreen = 14;          // after deleting the account: the login page, as on a fresh install

        const string ConfirmText = "Delete your online account?\n\nThis removes your online profile, rank, stats, photo, friends and blocked list from our servers and logs you out. It cannot be undone. Your sound settings stay on this phone.";

        bool busy;

        /// <summary>Only a logged-in phone that really has an online account has something to delete.</summary>
        static bool CanDelete => OnlineService.HasChosenLogin && OnlineService.HasAccount;

        void OnEnable()
        {
            policyModal.SetActive(false);
            deleteModal.SetActive(false);
            if (deleteAccountButton != null) deleteAccountButton.SetActive(CanDelete);
        }

        // ---------- privacy policy ----------

        public void OpenPolicy()
        {
            policyBody.text = PolicyText.Short;
            var config = OnlineConfig.Load();
            openOnlineButton.gameObject.SetActive(config != null && !string.IsNullOrEmpty(config.privacyPolicyUrl));
            policyModal.SetActive(true);
        }

        public void ClosePolicy() => policyModal.SetActive(false);

        public void OpenPolicyOnline()
        {
            var config = OnlineConfig.Load();
            if (config != null && !string.IsNullOrEmpty(config.privacyPolicyUrl)) Application.OpenURL(config.privacyPolicyUrl);
        }

        // ---------- delete account ----------

        public void OpenDelete()
        {
            deleteMessage.text = ConfirmText;
            deleteConfirmButton.gameObject.SetActive(true);
            deleteConfirmLabel.text = "Delete";
            deleteModal.SetActive(true);
        }

        public void CloseDelete()
        {
            if (!busy) deleteModal.SetActive(false);
        }

        public async void ConfirmDelete()
        {
            if (busy) return;
            busy = true;
            deleteMessage.text = "Deleting...";
            deleteConfirmButton.gameObject.SetActive(false);
            if (!CanDelete)
            {
                busy = false;
                deleteMessage.text = "You have no online account on this phone.";
                return;                                   // never went online: nothing to delete (and we must not create one just to delete it)
            }
            bool ok = await OnlineService.DeleteAccountAsync();
            busy = false;
            if (this == null) return;
            if (!ok)
            {
                deleteMessage.text = "Could not delete: " + OnlineService.LastError;
                deleteConfirmButton.gameObject.SetActive(true);
                deleteConfirmLabel.text = "Try again";
                return;
            }
            // the phone is now logged out: start again at the login page, with no way back into the old session
            deleteModal.SetActive(false);
            LoginGate.Purpose = LoginPurpose.Start;
            LoginGate.Notice = "Your online account was deleted.";
            router.ResetTo(welcomeScreen);
        }
    }

    /// <summary>The privacy policy in short form, shown inside the game. The full text is Documentation/Store/privacy-policy.html.</summary>
    public static class PolicyText
    {
        public const string Short =
            "PRIVACY POLICY (short)\n\n" +
            "Ludo Fight is for players aged 13 and over.\n\n" +
            "OFFLINE: names, pictures and settings stay on your phone.\n\n" +
            "ONLINE (optional): the game creates an anonymous online account. We use an anonymous player ID, your display name, your picture number, your room and friends, and match data, only to run online play.\n\n" +
            "VOICE (optional): starts only when you tap the microphone button, and Android asks first. Your voice goes live to the players in your room. It is not recorded or stored by us.\n\n" +
            "ADS: Google AdMob shows ads. It may use your advertising ID and approximate location. Where required, you are asked for consent, and you can change it in Settings > Privacy Settings.\n\n" +
            "SAFETY: you can mute, block or report any player.\n\n" +
            "DELETE: Settings > Delete online account removes your online data. You can also e-mail us.\n\n" +
            "We do not sell your data. Providers: Unity (online services), Google (ads).";
    }
}
