using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Play Online > Account: how this phone is signed in. A guest can log in with Google or Facebook here and keeps
    /// everything (the guest account is upgraded in place); anyone can log out. Logging IN from nothing happens on the one
    /// login page (WelcomeScreen) - this page is never a second login page.
    /// </summary>
    public sealed class AccountScreen : MonoBehaviour
    {
        [SerializeField] ScreenRouter router;
        [SerializeField] int onlineScreen = 8;
        [SerializeField] int welcomeScreen = 14;          // the login page: shown after logging out, or when Play Online needs a login
        [SerializeField] TMP_Text headline;               // "Signed in with Facebook" / "You are playing as a guest"
        [SerializeField] TMP_Text messageText;
        [SerializeField] GameObject guestNote;            // "A guest profile lives only on this phone..." (guests only)
        [SerializeField] Button logoutButton;
        [SerializeField] Button googleButton;
        [SerializeField] Button facebookButton;

        bool busy;
        string switchFor;                  // "Google"/"Facebook" while we wait for the player to confirm using the profile that already exists

        void OnEnable()
        {
            busy = false;
            messageText.text = "";
            Refresh();
        }

        void Refresh()
        {
            bool account = OnlineService.IsAccount;
            switchFor = null;
            googleButton.gameObject.SetActive(!account);
            facebookButton.gameObject.SetActive(!account && FacebookLogin.Enabled);      // Facebook: next version
            logoutButton.gameObject.SetActive(true);
            if (guestNote != null) guestNote.SetActive(!account);
            headline.text = account
                ? (OnlineService.ProviderLabel == "Account" ? "Signed in as " + OnlineService.AccountName : "Signed in with " + OnlineService.ProviderLabel)
                : "You are playing as a guest.\nLog in with " + (FacebookLogin.Enabled ? "Google or Facebook" : "Google") + " to keep your progress on any phone.";
        }

        /// <summary>The "Play Online" button in the mode menu: the login page when nobody is logged in, otherwise the online menu.</summary>
        public void OpenOnline()
        {
            if (OnlineService.HasChosenLogin) { router.Show(onlineScreen); return; }
            LoginGate.Purpose = LoginPurpose.Online;
            router.Show(welcomeScreen);
        }

        // ---------- buttons ----------

        public void Google() => RunSocial("Google", OnlineService.GoogleAsync, OnlineService.SwitchToGoogleAsync);

        public void Facebook() => RunSocial("Facebook", OnlineService.FacebookAsync, OnlineService.SwitchToFacebookAsync);

        async void RunSocial(string provider, System.Func<System.Threading.Tasks.Task<OnlineService.SocialResult>> first, System.Func<System.Threading.Tasks.Task<bool>> switchTo)
        {
            if (busy) return;
            bool second = switchFor == provider;             // second tap: the player agreed to use the profile that already exists
            switchFor = null;
            busy = true;
            SetButtons(false);
            messageText.color = Color.white;
            messageText.text = second ? "Switching to your " + provider + " profile..." : "Connecting to " + provider + "...";
            bool done;
            if (second) done = await switchTo();
            else
            {
                var result = await first();
                if (this == null) return;
                if (result == OnlineService.SocialResult.NeedsChoice)
                {
                    busy = false;
                    SetButtons(true);
                    switchFor = provider;
                    messageText.color = new Color(1f, 0.85f, 0.6f);
                    messageText.text = "That " + provider + " account already has a Ludo Fight profile. Tap " + provider + " again to use it (this guest profile stays behind).";
                    return;
                }
                done = result == OnlineService.SocialResult.Done;
            }
            busy = false;
            if (this == null) return;
            SetButtons(true);
            if (done) { router.Back(); return; }
            messageText.color = new Color(1f, 0.85f, 0.6f);
            messageText.text = OnlineService.LastError;
        }

        public async void LogOut()
        {
            if (busy) return;
            busy = true;
            SetButtons(false);
            messageText.color = Color.white;
            messageText.text = "Logging out...";
            await OnlineService.SignOutAsync();
            busy = false;
            if (this == null) return;
            LoginGate.Purpose = LoginPurpose.Start;          // logged out: the same page as at the start of the game
            LoginGate.Notice = "You have logged out.";
            router.ResetTo(welcomeScreen);
        }

        void SetButtons(bool on)
        {
            logoutButton.interactable = on;
            googleButton.interactable = on;
            facebookButton.interactable = on;
        }
    }
}
