using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// The one login page of the game: Continue with Google, Continue with Facebook or Continue as Guest. Shown on the first
    /// launch, after logging out or deleting the account, and when Play Online is tapped while nobody is logged in
    /// (LoginGate.Purpose says which). The guest button needs no internet.
    /// </summary>
    public sealed class WelcomeScreen : MonoBehaviour
    {
        [SerializeField] ScreenRouter router;
        [SerializeField] int mainMenuScreen = 1;
        [SerializeField] int settingsScreen = 5;
        [SerializeField] GameObject backdrop;             // the picture behind the page, kept outside the safe area so it fills the whole screen
        [SerializeField] int onlineScreen = 8;
        [SerializeField] GameObject backButton;           // top-left arrow, only when opened from Play Online (there is somewhere to go back to)
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text messageText;            // "Connecting to Google...", errors, the policy notice
        [SerializeField] Button googleButton;
        [SerializeField] Button facebookButton;
        [SerializeField] Button guestButton;

        const string StartSubtitle = "Login or create an account to start\nyour Ludo journey";
        const string OnlineSubtitle = "Log in to play online\nwith players everywhere";

        bool busy;
        string switchFor;           // "Google"/"Facebook" while we wait for the player to confirm using the profile that already exists
        LoginPurpose purpose;

        void OnEnable()
        {
            busy = false;
            switchFor = null;
            purpose = LoginGate.Purpose;
            if (backButton != null) backButton.SetActive(purpose == LoginPurpose.Online);
            if (subtitle != null) subtitle.text = purpose == LoginPurpose.Online ? OnlineSubtitle : StartSubtitle;
            SetButtons(true);
            if (backdrop != null) backdrop.SetActive(true);
            Show(LoginGate.Notice, false);
            LoginGate.Notice = "";
        }

        void OnDisable()
        {
            if (backdrop != null) backdrop.SetActive(false);
        }

        // ---------- buttons ----------

        /// <summary>Continue as a guest: no internet needed. Online play creates the guest identity when it is first opened.</summary>
        public void Guest()
        {
            if (busy) return;
            OnlineService.ChooseGuest();
            Finish();
        }

        public void Back()
        {
            if (busy) return;
            LoginGate.Purpose = LoginPurpose.Start;
            router.Back();
        }

        public void Google() => RunSocial("Google", OnlineService.GoogleAsync, OnlineService.SwitchToGoogleAsync);

        public void Facebook() => RunSocial("Facebook", OnlineService.FacebookAsync, OnlineService.SwitchToFacebookAsync);

        public void OpenSettings()
        {
            if (!busy) router.Show(settingsScreen);
        }

        /// <summary>"Terms of Service and Privacy Policy": opens the policy page once the owner has set its address in OnlineConfig.</summary>
        public void OpenPolicy()
        {
            var config = OnlineConfig.Load();
            if (config != null && !string.IsNullOrEmpty(config.privacyPolicyUrl)) Application.OpenURL(config.privacyPolicyUrl);
            else Show("The privacy policy address is added when the game is published.", false);
        }

        async void RunSocial(string provider, System.Func<Task<OnlineService.SocialResult>> first, System.Func<Task<bool>> switchTo)
        {
            if (busy) return;
            bool second = switchFor == provider;             // second tap: the player agreed to use the profile that already exists
            switchFor = null;
            busy = true;
            SetButtons(false);
            Show(second ? "Switching to your " + provider + " profile..." : "Connecting to " + provider + "...", false);

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
                    Show("That " + provider + " account already has a Ludo Fight profile. Tap " + provider + " again to use it (this guest profile stays behind).", true);
                    return;
                }
                done = result == OnlineService.SocialResult.Done;
            }
            busy = false;
            if (this == null) return;
            SetButtons(true);
            if (done) Finish();
            else Show(OnlineService.LastError, true);
        }

        void Finish()
        {
            LoginGate.Purpose = LoginPurpose.Start;
            if (purpose == LoginPurpose.Online) router.Replace(onlineScreen);   // Play Online asked for the login: carry on there
            else router.ResetTo(mainMenuScreen);
        }

        void Show(string text, bool problem)
        {
            messageText.color = problem ? new Color(1f, 0.85f, 0.6f) : Color.white;
            messageText.text = text;
        }

        void SetButtons(bool on)
        {
            googleButton.interactable = on;
            facebookButton.interactable = on;
            facebookButton.gameObject.SetActive(FacebookLogin.Enabled);        // Facebook: next version
            guestButton.interactable = on;
        }
    }
}
