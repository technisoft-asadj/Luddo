using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;
using Ludo.Online;

namespace Ludo.EditorTools
{
    /// <summary>
    /// The welcome (login) page, built from the designer's prototype (Prototype/login_signup page.png) with the game's own name.
    /// Its pictures are made by Prototype/make_welcome_art.py into Assets/_Game/Art/Welcome.
    /// </summary>
    public static partial class UiSceneBuilder
    {
        const string WelcomeArt = "Assets/_Game/Art/Welcome/";
        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        /// <summary>A picture from the Welcome art folder, imported as a sprite.</summary>
        static Sprite WelcomeSprite(string file)
        {
            string path = WelcomeArt + file;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.maxTextureSize < 4096))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return Load(path);
        }

        static RectTransform BuildWelcome(RectTransform parent, ScreenRouter router)
        {
            var s = NewScreen("Screen_Welcome", parent);
            var welcome = s.gameObject.AddComponent<WelcomeScreen>();

            // ----- background: the prototype's corners, covering the whole screen without stretching -----
            // it lives directly under the canvas (not the safe area) so it also fills the notch area at the top
            var bg = NewRect("WelcomeBackdrop", (RectTransform)parent.parent);
            bg.SetSiblingIndex(1);                                       // above the shared background, below every screen
            At(bg, Mid, Mid, Vector2.zero, new Vector2(1080f, 2430f));
            var bgImg = AddImage(bg, WelcomeSprite("welcome_bg.png"), Color.white); bgImg.raycastTarget = false;
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 941f / 2122f;

            // ----- top group: crown + pawns + dice, the name, the tagline (positions are measured from the middle of the screen) -----
            const float tagY = 400f;
            var emblem = NewRect("Emblem", s);
            At(emblem, Mid, Mid, new Vector2(0f, tagY + 392f), new Vector2(600f, 327f));
            AddImage(emblem, WelcomeSprite("welcome_emblem.png"), Color.white).raycastTarget = false;
            emblem.gameObject.AddComponent<Bob>();

            var wordmark = NewRect("Wordmark", s);
            At(wordmark, Mid, Mid, new Vector2(0f, tagY + 150f), new Vector2(900f, 230f));
            var wmImg = AddImage(wordmark, WelcomeSprite("welcome_wordmark.png"), Color.white);
            wmImg.raycastTarget = false; wmImg.preserveAspect = true;

            var tagline = AddText(s, "Tagline", "Play            Connect            Win", 50, Color.white, TextAlignmentOptions.Center);
            At(tagline.rectTransform, Mid, Mid, new Vector2(0f, tagY - 10f), new Vector2(940f, 70f));
            // the two coloured dots between the words
            for (int i = 0; i < 2; i++)
            {
                var dot = NewRect("TaglineDot" + i, s);
                At(dot, Mid, Mid, new Vector2(i == 0 ? -140f : 146f, tagY - 10f), new Vector2(22f, 22f));
                AddImage(dot, Circle(), i == 0 ? new Color(1f, 0.79f, 0.2f) : new Color(0.25f, 0.85f, 0.4f)).raycastTarget = false;
            }

            // ----- back arrow (top left): only shown when the page is opened from Account -----
            var back = MakeRoundButton(s, "BackButton", "Blue", Load(KenneyUi + "Icons/arrow_basic_w.png"), 110f, SfxId.Back);
            At((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -40f), new Vector2(110f, 110f));
            OnClick(back, welcome.Back);

            // ----- settings gear (top right) -----
            var gear = MakeRoundButton(s, "SettingsButton", "Blue", Icon("gear"), 110f);
            At((RectTransform)gear.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -40f), new Vector2(110f, 110f));

            // ----- welcome text -----
            var title = AddText(s, "Welcome", "Welcome!", 112, Color.white, TextAlignmentOptions.Center, true);
            At(title.rectTransform, Mid, Mid, new Vector2(0f, 150f), new Vector2(900f, 140f));
            var sub = AddText(s, "Subtitle", "Login or create an account to start\nyour Ludo journey", 46, new Color(0.86f, 0.92f, 1f, 0.92f), TextAlignmentOptions.Center);
            sub.textWrappingMode = TextWrappingModes.Normal;
            At(sub.rectTransform, Mid, Mid, new Vector2(0f, 20f), new Vector2(940f, 130f));

            // ----- buttons -----
            var google = MakeButton(s, "GoogleButton", "Continue with Google", "Grey", new Vector2(790f, 140f), null);
            At((RectTransform)google.transform, Mid, Mid, new Vector2(0f, FacebookLogin.Enabled ? -150f : -250f), new Vector2(790f, 140f));
            BrandIcon((RectTransform)google.transform, WelcomeSprite("icon_google.png"), 78f);

            var facebook = MakeButton(s, "FacebookButton", "Continue with Facebook", "Blue", new Vector2(790f, 140f), null);
            At((RectTransform)facebook.transform, Mid, Mid, new Vector2(0f, -320f), new Vector2(790f, 140f));
            BrandIcon((RectTransform)facebook.transform, WelcomeSprite("icon_facebook.png"), 78f);
            facebook.gameObject.SetActive(FacebookLogin.Enabled);        // not in this version (kept for the next one)

            // ----- OR divider -----
            var line1 = NewRect("OrLineLeft", s);
            At(line1, Mid, Mid, new Vector2(-235f, -450f), new Vector2(300f, 3f));
            AddImage(line1, null, new Color(1f, 1f, 1f, 0.35f)).raycastTarget = false;
            var line2 = NewRect("OrLineRight", s);
            At(line2, Mid, Mid, new Vector2(235f, -450f), new Vector2(300f, 3f));
            AddImage(line2, null, new Color(1f, 1f, 1f, 0.35f)).raycastTarget = false;
            var or = AddText(s, "Or", "OR", 42, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
            At(or.rectTransform, Mid, Mid, new Vector2(0f, -450f), new Vector2(160f, 60f));

            var guest = MakeButton(s, "GuestButton", "Continue as Guest", "Green", new Vector2(790f, 140f), Ico("person"));
            At((RectTransform)guest.transform, Mid, Mid, new Vector2(0f, -580f), new Vector2(790f, 140f));

            // ----- message + terms line (bottom) -----
            var message = AddText(s, "Message", "", 40, Color.white, TextAlignmentOptions.Center, true);
            message.textWrappingMode = TextWrappingModes.Normal;
            message.enableAutoSizing = true; message.fontSizeMin = 26f; message.fontSizeMax = 40f;
            At(message.rectTransform, Mid, Mid, new Vector2(0f, -735f), new Vector2(940f, 120f));

            var terms = NewRect("Terms", s);
            At(terms, BottomCenter, BottomCenter, new Vector2(0f, 270f), new Vector2(940f, 110f));
            var termsHit = AddImage(terms, null, new Color(0f, 0f, 0f, 0f)); termsHit.raycastTarget = true;
            var termsButton = terms.gameObject.AddComponent<Button>();
            termsButton.targetGraphic = termsHit; termsButton.transition = Selectable.Transition.None;
            var termsText = AddText(terms, "Text", "By continuing, you agree to our\n<color=#4DB2FF>Terms of Service</color> and <color=#4DB2FF>Privacy Policy</color>", 36,
                new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            termsText.textWrappingMode = TextWrappingModes.Normal;
            Stretch(termsText.rectTransform);

            OnClick(google, welcome.Google);
            OnClick(facebook, welcome.Facebook);
            OnClick(guest, welcome.Guest);
            OnClick(gear, welcome.OpenSettings);
            OnClick(termsButton, welcome.OpenPolicy);

            var so = new SerializedObject(welcome);
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("mainMenuScreen").intValue = Main;
            so.FindProperty("settingsScreen").intValue = Settings;
            so.FindProperty("onlineScreen").intValue = Online;
            so.FindProperty("backdrop").objectReferenceValue = bg.gameObject;
            so.FindProperty("backButton").objectReferenceValue = back.gameObject;
            so.FindProperty("subtitle").objectReferenceValue = sub;
            bg.gameObject.SetActive(false);                              // the page switches it on when it opens
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("googleButton").objectReferenceValue = google;
            so.FindProperty("facebookButton").objectReferenceValue = facebook;
            so.FindProperty("guestButton").objectReferenceValue = guest;
            so.ApplyModifiedProperties();
            return s;
        }

        /// <summary>The Google / Facebook logo on the left of a button (kept in its own colours).</summary>
        static void BrandIcon(RectTransform button, Sprite logo, float size)
        {
            var ic = NewRect("BrandIcon", button);
            At(ic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, 5f), new Vector2(size, size));
            var img = AddImage(ic, logo, Color.white);
            img.raycastTarget = false; img.preserveAspect = true;
            var label = button.Find("Label");                       // keep the words clear of the logo
            if (label != null) Stretch((RectTransform)label, 140f, 0f, 40f, 10f);
        }
    }
}
