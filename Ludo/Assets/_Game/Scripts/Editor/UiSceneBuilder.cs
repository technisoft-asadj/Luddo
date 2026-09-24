using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Builds the UI scenes (Menu, and the HUD inside the Game scene) from the imported art. Run it from the
    /// menu Ludo > Build ... or call it from code. After it has run, everything is normal editable Unity content:
    /// you can move objects, change colours and texts in the Hierarchy/Inspector. Re-running REPLACES the scene.
    ///
    /// Visual language: a 3D "lip" under every button and card (a darker copy of the shape shifted down), a glossy
    /// highlight on top, outlined labels, soft shadows, a layered gradient background with slowly drifting dice.
    /// </summary>
    public static partial class UiSceneBuilder
    {
        const string KenneyUi = "Assets/ThirdParty/Kenney/UI/";
        const string Generated = UiArtGenerator.Folder;
        const string MenuScenePath = "Assets/_Game/Scenes/Menu.unity";
        const string GameScenePath = "Assets/_Game/Scenes/Game.unity";

        // ----- palette -----
        static readonly Color BackgroundBase = new Color(0.08f, 0.24f, 0.66f);
        static readonly Color Card = new Color(0.94f, 0.97f, 1f);
        static readonly Color CardLip = new Color(0.58f, 0.70f, 0.93f);
        static readonly Color Navy = new Color(0.08f, 0.18f, 0.45f);
        static readonly Color SubText = new Color(0.32f, 0.42f, 0.64f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.15f);
        static readonly Color DarkPanel = new Color(0.10f, 0.19f, 0.47f);
        static readonly Color DarkPanelLip = new Color(0.04f, 0.09f, 0.28f);

        struct ButtonStyle
        {
            public Color face, lip;
            public bool darkLabel;
            public ButtonStyle(Color face, Color lip, bool darkLabel = false) { this.face = face; this.lip = lip; this.darkLabel = darkLabel; }
        }

        static ButtonStyle Style(string name)
        {
            switch (name)
            {
                case "Green": return new ButtonStyle(new Color(0.22f, 0.80f, 0.32f), new Color(0.08f, 0.45f, 0.15f));
                case "Blue": return new ButtonStyle(new Color(0.20f, 0.56f, 1f), new Color(0.07f, 0.30f, 0.72f));
                case "Purple": return new ButtonStyle(new Color(0.66f, 0.40f, 0.96f), new Color(0.38f, 0.17f, 0.64f));
                case "Orange": return new ButtonStyle(new Color(1f, 0.68f, 0.14f), new Color(0.78f, 0.38f, 0.04f));
                case "Red": return new ButtonStyle(new Color(0.96f, 0.30f, 0.32f), new Color(0.62f, 0.10f, 0.14f));
                default: return new ButtonStyle(new Color(0.90f, 0.94f, 1f), new Color(0.55f, 0.64f, 0.82f), true);   // Grey
            }
        }

        // screen numbers (must match the buttons and MenuFlow)
        const int Splash = 0, Main = 1, Mode = 2, Players = 3, Difficulty = 4, Settings = 5, HowTo = 6, Credits = 7, Online = 8, Room = 9, Friends = 10, Account = 11, Leaderboards = 12, Profile = 13, Welcome = 14, QuickMatch = 15;

        // ==================================================================================================
        //  MENU SCENE
        // ==================================================================================================

        [MenuItem("Ludo/Build Menu Scene")]
        public static void BuildMenuScene()
        {
            PrepareSprites();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera cam = MakeCamera();
            Canvas canvas = MakeCanvas(cam, "Canvas", 0);
            MakeEventSystem();
            RectTransform root = (RectTransform)canvas.transform;

            BuildBackground(root, true);
            var safe = NewRect("SafeArea", root); Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var systems = new GameObject("Systems");
            var router = systems.AddComponent<ScreenRouter>();
            var flow = systems.AddComponent<MenuFlow>();

            var screens = new RectTransform[16];
            screens[Splash] = BuildSplash(safe, router);
            screens[Main] = BuildMain(safe, router, flow);
            screens[Mode] = BuildMode(safe, router, flow);
            var playersScreen = BuildPlayers(safe, router, flow, out var rows, out var rowNames, out var rowPawns, out var rowAvatars, out var editButtons);
            screens[Players] = playersScreen;
            screens[Difficulty] = BuildDifficulty(safe, router, flow, out var cards, out var checks, out var oppButtons);
            screens[Settings] = BuildSettings(safe, root, router, out var settingsModals);
            screens[HowTo] = BuildHowTo(safe, router);
            screens[Credits] = BuildCredits(safe, router);

            // online: hub, waiting room, friends (+ their pop-ups, which sit above every screen)
            screens[Online] = BuildOnline(safe, root, router, out var onlineMenu, out var joinModal, out var busyOverlay);
            screens[Room] = BuildRoom(safe, root, router, out var roomScreen, out var moreModal);
            screens[Friends] = BuildFriends(safe, router);
            screens[Account] = BuildAccount(safe, router, out var accountScreen);
            screens[Leaderboards] = BuildLeaderboards(safe, router);
            screens[Profile] = BuildProfile(safe, router, flow);
            screens[Welcome] = BuildWelcome(safe, router);
            screens[QuickMatch] = BuildQuickMatch(safe, router);
            // "Play Online" in the mode menu opens the login page the first time, the online menu afterwards
            OnClick(screens[Mode].Find("CardOnline").GetComponent<Button>(), accountScreen.OpenOnline);
            var inviteModal = BuildInviteModal(root, router);

            // main-menu banner: shown only while the Main screen is on show
            var banner = systems.AddComponent<MenuBanner>();
            var bannerObj = new SerializedObject(banner);
            bannerObj.FindProperty("router").objectReferenceValue = router;
            bannerObj.FindProperty("mainScreen").intValue = Main;
            bannerObj.ApplyModifiedProperties();

            var nameModal = BuildProfileEditor(root, out var profileEditor, out var okButton, out var cancelButton);
            var countryModal = BuildCountryPicker(root, out var countryPicker);        // above the profile pop-up
            var peo = new SerializedObject(profileEditor);
            peo.FindProperty("countryPicker").objectReferenceValue = countryPicker;
            peo.ApplyModifiedProperties();
            var omo = new SerializedObject(onlineMenu);
            omo.FindProperty("countryPicker").objectReferenceValue = countryPicker;
            omo.ApplyModifiedProperties();
            OnClick(okButton, profileEditor.Confirm);
            OnClick(cancelButton, profileEditor.Cancel);
            for (int i = 0; i < editButtons.Length; i++) OnClickInt(editButtons[i], flow.EditProfile, i);

            // hand the pieces to the scripts
            var so = new SerializedObject(router);
            SetObjects(so.FindProperty("screens"), screens);
            SetObjects(so.FindProperty("modals"), new[] { countryModal, root.Find("DailyRewardModal").gameObject, nameModal, joinModal, moreModal, inviteModal, settingsModals[0], settingsModals[1] });
            so.FindProperty("firstScreen").intValue = Splash;
            so.ApplyModifiedProperties();

            var fo = new SerializedObject(flow);
            fo.FindProperty("router").objectReferenceValue = router;
            fo.FindProperty("playerSelectScreen").intValue = Players;
            fo.FindProperty("difficultyScreen").intValue = Difficulty;
            fo.FindProperty("profileEditor").objectReferenceValue = profileEditor;
            SetObjects(fo.FindProperty("playerRows"), rows);
            SetObjects(fo.FindProperty("playerRowNames"), rowNames);
            SetObjects(fo.FindProperty("playerRowPawns"), rowPawns);
            SetObjects(fo.FindProperty("playerRowAvatars"), rowAvatars);
            SetObjects(fo.FindProperty("difficultyCards"), cards);
            SetObjects(fo.FindProperty("difficultyChecks"), checks);
            SetObjects(fo.FindProperty("opponentButtons"), oppButtons);
            fo.ApplyModifiedProperties();

            for (int i = 0; i < screens.Length; i++) screens[i].gameObject.SetActive(i == Splash);
            nameModal.SetActive(false);
            countryModal.SetActive(false);
            joinModal.SetActive(false);
            moreModal.SetActive(false);
            inviteModal.SetActive(false);
            busyOverlay.SetActive(false);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MenuScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
            Debug.Log("[Ludo] Menu scene built.");
        }

        // ---------- individual menu screens ----------

        static RectTransform BuildSplash(RectTransform parent, ScreenRouter router)
        {
            var s = NewScreen("Screen_Splash", parent);
            Logo(s, new Vector2(0.5f, 1f), new Vector2(0f, -330f), 820f);
            var tagline = AddText(s, "Tagline", "Play            Connect            Win", 54, Color.white, TextAlignmentOptions.Center, true);
            At(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1080f), new Vector2(940f, 80f));
            for (int i = 0; i < 2; i++)                          // the same coloured dots as on the welcome page
            {
                var dot = NewRect("TaglineDot" + i, s);
                At(dot, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -150f : 156f, -1080f), new Vector2(24f, 24f));
                AddImage(dot, Circle(), i == 0 ? new Color(1f, 0.79f, 0.2f) : new Color(0.25f, 0.85f, 0.4f)).raycastTarget = false;
            }

            var track = NewRect("LoadingBar", s);
            At(track, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(720f, 48f));
            var trackImg = AddImage(track, Round(), new Color(0.02f, 0.08f, 0.28f, 0.85f), true, 0.9f);
            Depth(trackImg, new Color(0.35f, 0.55f, 1f, 0.35f), 4f, 0f);
            var clip = NewRect("FillMask", track); Stretch(clip, 6f, 6f, 6f, 6f);
            AddImage(clip, Round(), Color.white, true, 0.9f);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var fill = NewRect("Fill", clip); Stretch(fill);
            var fillImage = AddImage(fill, null, new Color(0.30f, 0.88f, 0.40f));
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;
            var shine = NewRect("Shine", clip);
            shine.anchorMin = new Vector2(0f, 0.5f); shine.anchorMax = Vector2.one; shine.offsetMin = shine.offsetMax = Vector2.zero;
            AddImage(shine, Load(Generated + "gradient_v.png"), new Color(1f, 1f, 1f, 0.45f)).raycastTarget = false;

            var splash = s.gameObject.AddComponent<SplashScreen>();
            var so = new SerializedObject(splash);
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("bar").objectReferenceValue = fillImage;
            so.FindProperty("mainMenuScreen").intValue = Main;
            so.FindProperty("loginScreen").intValue = Welcome;
            so.ApplyModifiedProperties();
            return s;
        }

        static RectTransform BuildMain(RectTransform parent, ScreenRouter router, MenuFlow flow)
        {
            var s = NewScreen("Screen_Main", parent);
            // the logo sits well below the profile tag (top-left), so the two never touch even while the logo floats
            Logo(s, new Vector2(0.5f, 1f), new Vector2(0f, -290f), 760f);

            // "picture + name" tag (top-left): shows the saved profile of Player 1 and opens the profile editor
            var chip = NewRect("ProfileChip", s);
            At(chip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(360f, 104f));
            var chipBody = AddImage(chip, Round(), new Color(0.05f, 0.11f, 0.33f, 0.85f), true, 0.8f);
            Depth(chipBody, new Color(0.02f, 0.05f, 0.2f), 6f);
            var chipButton = chip.gameObject.AddComponent<Button>();
            chipButton.targetGraphic = chipBody;
            chipButton.transition = Selectable.Transition.None;
            AddButtonFx(chip.gameObject, false);
            OnClickInt(chipButton, flow.EditProfile, 0);
            var chipRing = NewRect("Ring", chip); Stretch(chipRing);
            AddImage(chipRing, Ring(), new Color(0.45f, 0.65f, 1f, 0.8f), true, 0.8f).raycastTarget = false;
            var chipAvatarBg = NewRect("AvatarBg", chip);
            At(chipAvatarBg, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(82f, 82f));
            AddImage(chipAvatarBg, Circle(), new Color(0.92f, 0.95f, 1f)).raycastTarget = false;
            var chipAvatar = NewRect("Avatar", chipAvatarBg); Stretch(chipAvatar, 5f, 5f, 5f, 5f);
            var chipAvatarImg = AddImage(chipAvatar, null, Color.white); chipAvatarImg.raycastTarget = false; chipAvatarImg.preserveAspect = true;
            var chipName = AddText(chip, "Name", "Player 1", 44, Color.white, TextAlignmentOptions.Left, true);
            Stretch(chipName.rectTransform, 108f, 0f, 20f, 4f);
            chipName.enableAutoSizing = true; chipName.fontSizeMin = 24f; chipName.fontSizeMax = 44f;
            var chipScript = chip.gameObject.AddComponent<ProfileChip>();
            var cso = new SerializedObject(chipScript);
            cso.FindProperty("player").intValue = 0;
            cso.FindProperty("avatar").objectReferenceValue = chipAvatarImg;
            cso.FindProperty("nameText").objectReferenceValue = chipName;
            cso.ApplyModifiedProperties();

            var play = MakeButton(s, "PlayButton", "Play", "Green", new Vector2(800f, 190f), Icon2("icon_play_light"), true);
            // the stack starts at 43% of the screen height (leaves room for the ad banner below) and keeps fixed gaps, so tall and short phones both look balanced
            At((RectTransform)play.transform, new Vector2(0.5f, 0.43f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 190f));
            OnClickInt(play, router.Show, Mode);

            var how = MakeButton(s, "HowToPlayButton", "How to Play", "Blue", new Vector2(800f, 165f), Icon("question"));
            At((RectTransform)how.transform, new Vector2(0.5f, 0.43f), new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(800f, 165f));
            OnClickInt(how, router.Show, HowTo);

            var settings = MakeButton(s, "SettingsButton", "Settings", "Purple", new Vector2(800f, 165f), Icon("gear"));
            At((RectTransform)settings.transform, new Vector2(0.5f, 0.43f), new Vector2(0.5f, 0.5f), new Vector2(0f, -445f), new Vector2(800f, 165f));
            OnClickInt(settings, router.Show, Settings);

            // quick mute buttons for music and sound effects: top-right corner, mirroring the profile tag on the left.
            // (The bottom of the screen belongs to the ad banner, so nothing tappable sits near it.)
            var musicBtn = MakeRoundButton(s, "MusicToggle", "Grey", Icon("musicOn"), 108f);
            At((RectTransform)musicBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-152f, -32f), new Vector2(108f, 108f));
            var sfxBtn = MakeRoundButton(s, "SfxToggle", "Grey", Icon("audioOn"), 108f);
            At((RectTransform)sfxBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -32f), new Vector2(108f, 108f));
            var quick = s.gameObject.AddComponent<QuickAudioToggles>();
            var qo = new SerializedObject(quick);
            qo.FindProperty("musicButton").objectReferenceValue = musicBtn;
            qo.FindProperty("musicIcon").objectReferenceValue = musicBtn.transform.Find("Icon").GetComponent<Image>();
            qo.FindProperty("sfxButton").objectReferenceValue = sfxBtn;
            qo.FindProperty("sfxIcon").objectReferenceValue = sfxBtn.transform.Find("Icon").GetComponent<Image>();
            qo.FindProperty("musicOn").objectReferenceValue = Icon("musicOn");
            qo.FindProperty("musicOff").objectReferenceValue = Icon("musicOff");
            qo.FindProperty("sfxOn").objectReferenceValue = Icon("audioOn");
            qo.FindProperty("sfxOff").objectReferenceValue = Icon("audioOff");
            qo.ApplyModifiedProperties();
            return s;
        }

        static RectTransform BuildMode(RectTransform parent, ScreenRouter router, MenuFlow flow)
        {
            var s = NewScreen("Screen_Mode", parent);
            Header(s, "Select Mode", router);
            var online = CardButton(s, "CardOnline", -300f, Ico("public"), "Play Online", "Friends, voice chat & quick match", new Color(0.96f, 0.30f, 0.32f), true);
            var two = CardButton(s, "Card2Players", -535f, Icon("multiplayer"), "2 Players", "Local (Pass & Play)", new Color(0.22f, 0.58f, 1f), true);
            var three = CardButton(s, "Card3Players", -770f, Icon("multiplayer"), "3 Players", "Local (Pass & Play)", new Color(1f, 0.66f, 0.14f), true);
            var four = CardButton(s, "Card4Players", -1005f, Icon("multiplayer"), "4 Players", "Local (Pass & Play)", new Color(0.66f, 0.40f, 0.96f), true);
            var ai = CardButton(s, "CardVsComputer", -1240f, Robot(), "vs Computer", "Play against the computer", new Color(0.22f, 0.78f, 0.34f), true);
            // (the "Play Online" card is wired after the login page exists: see BuildMenuScene)
            OnClickInt(two, flow.ChooseLocal, 2);
            OnClickInt(three, flow.ChooseLocal, 3);
            OnClickInt(four, flow.ChooseLocal, 4);
            OnClick(ai, flow.ChooseVsComputer);
            return s;
        }

        static RectTransform BuildPlayers(RectTransform parent, ScreenRouter router, MenuFlow flow,
            out GameObject[] rows, out TMP_Text[] names, out Image[] pawns, out Image[] avatars, out Button[] editButtons)
        {
            var s = NewScreen("Screen_Players", parent);
            Header(s, "Select Players", router);
            rows = new GameObject[4]; names = new TMP_Text[4]; pawns = new Image[4]; avatars = new Image[4]; editButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var row = NewRect("PlayerRow" + (i + 1), s);
                At(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f - i * 220f), new Vector2(940f, 170f));
                var body = AddImage(row, Round(), Card, true, 0.5f);
                Depth(body, CardLip, 10f);

                // the player's picture, with a small pawn in the seat colour hanging off its corner
                var avatar = NewRect("Avatar", row);
                At(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 4f), new Vector2(124f, 124f));
                var ring = AddImage(avatar, Circle(), new Color(0.80f, 0.87f, 1f));
                ring.raycastTarget = false;
                var picture = NewRect("Picture", avatar); Stretch(picture, 8f, 8f, 8f, 8f);
                avatars[i] = AddImage(picture, null, Color.white);
                avatars[i].raycastTarget = false; avatars[i].preserveAspect = true;
                var pawnBadge = NewRect("PawnBadge", avatar);
                At(pawnBadge, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-8f, 8f), new Vector2(54f, 54f));
                var pawnBadgeImg = AddImage(pawnBadge, Circle(), Color.white); pawnBadgeImg.raycastTarget = false;
                Depth(pawnBadgeImg, new Color(0f, 0f, 0.2f, 0.35f), 3f, 0f);
                var pawn = NewRect("Pawn", pawnBadge);
                At(pawn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(38f, 38f));
                pawns[i] = AddImage(pawn, Pawn(), SeatStyle.Colors[i]);
                pawns[i].raycastTarget = false;

                var label = AddText(row, "Name", "Player " + (i + 1), 60, Navy, TextAlignmentOptions.Left);
                At(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(190f, 0f), new Vector2(480f, 100f));
                label.enableAutoSizing = true; label.fontSizeMin = 32f; label.fontSizeMax = 60f;
                var edit = MakeButton(row, "EditButton", "Edit", "Grey", new Vector2(190f, 96f), null);
                At((RectTransform)edit.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 4f), new Vector2(190f, 96f));
                rows[i] = row.gameObject; names[i] = label; editButtons[i] = edit;
            }
            BuildModePicker(s, -1165f, withRule: true);
            var start = MakeButton(s, "StartGameButton", "Start Game", "Green", new Vector2(800f, 170f), Icon2("icon_play_light"), true);
            At((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(800f, 170f));
            OnClick(start, flow.StartLocalGame);
            return s;
        }

        static RectTransform BuildDifficulty(RectTransform parent, ScreenRouter router, MenuFlow flow,
            out Image[] cards, out GameObject[] checks, out Image[] oppButtons)
        {
            var s = NewScreen("Screen_Difficulty", parent);
            Header(s, "Select Difficulty", router);
            string[] titles = { "Easy", "Medium", "Hard" };
            string[] subs = { "Simple moves", "Balanced strategy", "Smart & aggressive" };
            Color[] accents = { new Color(0.22f, 0.78f, 0.34f), new Color(1f, 0.66f, 0.14f), new Color(0.96f, 0.30f, 0.32f) };
            cards = new Image[3]; checks = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var b = CardButton(s, "Card" + titles[i], -300f - i * 235f, Robot(), titles[i], subs[i], accents[i], false);
                OnClickInt(b, flow.SetDifficulty, i);
                cards[i] = b.GetComponent<Image>();

                // "selected" marker: a green outline round the card and a tick badge (hidden until chosen)
                var mark = NewRect("Check", b.transform); Stretch(mark);
                var outline = NewRect("Outline", mark); Stretch(outline, -6f, -6f, -6f, -6f);
                AddImage(outline, Ring(), new Color(0.20f, 0.78f, 0.32f), true, 0.5f).raycastTarget = false;
                var badge = NewRect("Badge", mark);
                At(badge, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(84f, 84f));
                var badgeImg = AddImage(badge, Circle(), new Color(0.20f, 0.78f, 0.32f)); badgeImg.raycastTarget = false;
                Depth(badgeImg, new Color(0.08f, 0.45f, 0.15f), 5f, 0f);
                var tick = NewRect("Tick", badge);
                At(tick, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
                AddImage(tick, Icon("checkmark"), Color.white).raycastTarget = false;
                checks[i] = mark.gameObject;
            }
            var label = AddText(s, "OpponentsLabel", "Opponents", 58, Color.white, TextAlignmentOptions.Left, true);
            At(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(70f, -1110f), new Vector2(500f, 90f));
            oppButtons = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var b = SmallChoice(s, "Opponents" + (i + 1), (i + 1).ToString(), new Vector2(440f + i * 190f, -1110f));
                OnClickInt(b, flow.SetOpponents, i + 1);
                oppButtons[i] = b.GetComponent<Image>();
            }
            BuildModePicker(s, -1215f, withRule: true);
            var start = MakeButton(s, "StartGameButton", "Start Game", "Green", new Vector2(800f, 170f), Icon2("icon_play_light"), true);
            At((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(800f, 170f));
            OnClick(start, flow.StartComputerGame);
            return s;
        }

        static RectTransform BuildSettings(RectTransform parent, RectTransform root, ScreenRouter router, out GameObject[] modals)
        {
            var s = NewScreen("Screen_Settings", parent);
            Header(s, "Settings", router);

            var panel = NewRect("Panel", s);
            At(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(940f, 700f));
            Depth(AddImage(panel, Round(), Card, true, 0.4f), CardLip, 12f);

            SectionTitle(panel, "Sound", -40f);
            var music = SliderRow(panel, "Music", Icon("musicOn"), -150f);
            var sfx = SliderRow(panel, "SFX", Icon("audioOn"), -290f);
            SectionTitle(panel, "Game", -430f);
            var vibration = ToggleRow(panel, "Vibration", Icon("power"), -540f);

            // the buttons stack in one column, so a hidden button (privacy settings, delete account) leaves no gap
            var column = NewRect("Buttons", s);
            At(column, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1030f), new Vector2(820f, 700f));
            var stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childAlignment = TextAnchor.UpperCenter;
            stack.spacing = 34f;
            stack.childControlWidth = stack.childControlHeight = false;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;

            var credits = MakeButton(column, "CreditsButton", "Credits", "Blue", new Vector2(800f, 140f), Icon("information"));
            OnClickInt(credits, router.Show, Credits);

            // privacy policy inside the app + delete the online account (Google Play requires both for apps with accounts)
            var online = s.gameObject.AddComponent<Ludo.Online.OnlineSettings>();
            var policyButton = MakeButton(column, "PolicyButton", "Privacy Policy", "Grey", new Vector2(800f, 140f), Icon("information"));
            OnClick(policyButton, online.OpenPolicy);

            // shown only where the law requires a privacy entry (SettingsScreen hides it otherwise)
            var privacy = MakeButton(column, "PrivacyButton", "Privacy Settings", "Grey", new Vector2(800f, 140f), null);

            var deleteButton = MakeButton(column, "DeleteAccountButton", "Delete Online Account", "Red", new Vector2(800f, 140f), Ico("block"));
            OnClick(deleteButton, online.OpenDelete);
            foreach (RectTransform child in column) child.sizeDelta = new Vector2(800f, 140f);

            // pop-up: the policy text
            var policyModal = NewRect("PolicyModal", root); Stretch(policyModal);
            AddImage(policyModal, null, new Color(0f, 0f, 0.05f, 0.78f)).raycastTarget = true;
            var pCard = NewRect("Card", policyModal);
            At(pCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1500f));
            Depth(AddImage(pCard, Round(), Card, true, 0.45f), CardLip, 14f);
            var pTitle = AddText(pCard, "Title", "Privacy Policy", 66, Navy, TextAlignmentOptions.Center);
            At(pTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 90f));
            var pScroll = NewRect("Scroll", pCard);
            At(pScroll, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(840f, 1050f));
            var scrollRect = pScroll.gameObject.AddComponent<ScrollRect>();
            var pView = NewRect("Viewport", pScroll); Stretch(pView);
            AddImage(pView, null, new Color(0f, 0f, 0f, 0f));
            pView.gameObject.AddComponent<RectMask2D>();
            var pContent = NewRect("Content", pView);
            pContent.anchorMin = new Vector2(0f, 1f); pContent.anchorMax = new Vector2(1f, 1f); pContent.pivot = new Vector2(0.5f, 1f);
            pContent.offsetMin = pContent.offsetMax = Vector2.zero;
            var pBody = AddText(pContent, "Body", "", 38, Navy, TextAlignmentOptions.TopLeft);
            pBody.textWrappingMode = TextWrappingModes.Normal;
            pBody.rectTransform.anchorMin = new Vector2(0f, 1f); pBody.rectTransform.anchorMax = new Vector2(1f, 1f); pBody.rectTransform.pivot = new Vector2(0.5f, 1f);
            pBody.rectTransform.offsetMin = pBody.rectTransform.offsetMax = Vector2.zero;
            pContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var fitBody = pBody.gameObject.AddComponent<ContentSizeFitter>(); fitBody.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = pView; scrollRect.content = pContent; scrollRect.horizontal = false; scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic; scrollRect.scrollSensitivity = 40f;
            var pOnline = MakeButton(pCard, "OpenOnlineButton", "Open full policy", "Blue", new Vector2(700f, 120f), null);
            At((RectTransform)pOnline.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(700f, 120f));
            var pClose = MakeButton(pCard, "CloseButton", "Close", "Grey", new Vector2(700f, 120f), null);
            At((RectTransform)pClose.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(700f, 120f));
            OnClick(pOnline, online.OpenPolicyOnline);
            OnClick(pClose, online.ClosePolicy);
            PopIn(policyModal, pCard);

            // pop-up: confirm deleting the online account
            var delModal = NewRect("DeleteModal", root); Stretch(delModal);
            AddImage(delModal, null, new Color(0f, 0f, 0.05f, 0.78f)).raycastTarget = true;
            var dCard = NewRect("Card", delModal);
            At(dCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 820f));
            Depth(AddImage(dCard, Round(), Card, true, 0.45f), CardLip, 14f);
            var dTitle = AddText(dCard, "Title", "Delete Account", 66, new Color(0.85f, 0.2f, 0.2f), TextAlignmentOptions.Center);
            At(dTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 90f));
            var dMsg = AddText(dCard, "Message", "", 40, Navy, TextAlignmentOptions.Center);
            dMsg.textWrappingMode = TextWrappingModes.Normal;
            At(dMsg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(800f, 460f));
            dMsg.enableAutoSizing = true; dMsg.fontSizeMin = 26f; dMsg.fontSizeMax = 40f;
            var dConfirm = MakeButton(dCard, "ConfirmButton", "Delete", "Red", new Vector2(360f, 130f), null);
            At((RectTransform)dConfirm.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(205f, 50f), new Vector2(360f, 130f));
            var dCancel = MakeButton(dCard, "CancelButton", "Close", "Grey", new Vector2(360f, 130f), null);
            At((RectTransform)dCancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-205f, 50f), new Vector2(360f, 130f));
            OnClick(dConfirm, online.ConfirmDelete);
            OnClick(dCancel, online.CloseDelete);
            PopIn(delModal, dCard);

            var oso = new SerializedObject(online);
            oso.FindProperty("policyModal").objectReferenceValue = policyModal.gameObject;
            oso.FindProperty("openOnlineButton").objectReferenceValue = pOnline;
            oso.FindProperty("policyBody").objectReferenceValue = pBody;
            oso.FindProperty("deleteModal").objectReferenceValue = delModal.gameObject;
            oso.FindProperty("deleteMessage").objectReferenceValue = dMsg;
            oso.FindProperty("deleteConfirmButton").objectReferenceValue = dConfirm;
            oso.FindProperty("deleteConfirmLabel").objectReferenceValue = dConfirm.transform.Find("Label").GetComponent<TMP_Text>();
            oso.FindProperty("deleteAccountButton").objectReferenceValue = deleteButton.gameObject;
            oso.FindProperty("router").objectReferenceValue = router;
            oso.FindProperty("welcomeScreen").intValue = Welcome;
            oso.ApplyModifiedProperties();
            policyModal.gameObject.SetActive(false);
            delModal.gameObject.SetActive(false);
            modals = new[] { policyModal.gameObject, delModal.gameObject };

            var screen = s.gameObject.AddComponent<SettingsScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("privacyButton").objectReferenceValue = privacy;
            so.FindProperty("music").objectReferenceValue = music;
            so.FindProperty("sfx").objectReferenceValue = sfx;
            so.FindProperty("vibration").objectReferenceValue = vibration;
            so.ApplyModifiedProperties();
            return s;
        }

        static RectTransform BuildHowTo(RectTransform parent, ScreenRouter router)
        {
            var s = NewScreen("Screen_HowTo", parent);
            Header(s, "How to Play", router);
            var panel = NewRect("Panel", s);
            At(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(940f, 1250f));
            Depth(AddImage(panel, Round(), Card, true, 0.4f), CardLip, 12f);

            var board = NewRect("BoardPicture", panel);
            At(board, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(430f, 430f));
            Depth(AddImage(board, Load(BoardArtGenerator.Folder + "board.png"), Color.white), new Color(0f, 0f, 0f, 0.25f), 8f, 0f);

            string[] steps =
            {
                "Roll the dice and move your token.",
                "Get all 4 tokens to the center.",
                "Capture opponent tokens.",
                "First to finish wins!"
            };
            Color[] dots = { new Color(0.22f, 0.58f, 1f), new Color(0.22f, 0.78f, 0.34f), new Color(1f, 0.66f, 0.14f), new Color(0.66f, 0.40f, 0.96f) };
            for (int i = 0; i < steps.Length; i++)
            {
                var dot = NewRect("Step" + (i + 1), panel);
                At(dot, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(50f, -600f - i * 150f), new Vector2(96f, 96f));
                var dotImg = AddImage(dot, Circle(), dots[i]);
                Depth(dotImg, Color.Lerp(dots[i], Color.black, 0.45f), 6f, 0.2f);
                var n = AddText(dot, "Number", (i + 1).ToString(), 58, Color.white, TextAlignmentOptions.Center, true);
                Stretch(n.rectTransform);
                var t = AddText(panel, "StepText" + (i + 1), steps[i], 46, Navy, TextAlignmentOptions.Left);
                At(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(175f, -600f - i * 150f), new Vector2(720f, 120f));
            }
            return s;
        }

        static RectTransform BuildCredits(RectTransform parent, ScreenRouter router)
        {
            var s = NewScreen("Screen_Credits", parent);
            Header(s, "Credits", router);
            var panel = NewRect("Panel", s);
            At(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(940f, 1250f));
            Depth(AddImage(panel, Round(), Card, true, 0.4f), CardLip, 12f);
            var text = AddText(panel, "Text",
                "<size=54><b>Ludo Fight</b></size>\nVersion {version}\n© 2026 Trisoftic. All rights reserved.\n\n" +
                "Sprites, dice, icons and sounds by Kenney (CC0)\nkenney.nl\n\n" +
                "Music: \"Happy Clappy Loop\" by OwlishMedia (CC0)\n\n" +
                "Font: Lilita One (SIL Open Font License)\n\n" +
                "Icons: Material Icons by Google (Apache 2.0)\n\n" +
                "Chat emojis: Noto Emoji by Google (Apache 2.0)\n\n" +
                "Online services: Unity Gaming Services\nAds: Google AdMob\n\n" +
                "Contact: trisoftic@gmail.com",
                40, Navy, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            Stretch(text.rectTransform, 45f, 45f, 45f, 45f);
            text.gameObject.AddComponent<VersionText>();
            return s;
        }

        /// <summary>The profile pop-up: big preview of the chosen picture, name field, a grid of avatars, OK / Cancel.</summary>
        static GameObject BuildProfileEditor(RectTransform root, out ProfileEditor editor, out Button ok, out Button cancel)
        {
            var modal = NewRect("ProfileEditorModal", root); Stretch(modal);
            var dim = AddImage(modal, null, new Color(0f, 0f, 0.05f, 0.72f));
            dim.raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 1540f));
            Depth(AddImage(card, Round(), Card, true, 0.45f), CardLip, 14f);
            var title = AddText(card, "Title", "Your Profile", 68, Navy, TextAlignmentOptions.Center);
            At(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -35f), new Vector2(700f, 95f));

            // big preview of the chosen picture
            var previewBg = NewRect("PreviewBg", card);
            At(previewBg, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(240f, 240f));
            var previewBgImg = AddImage(previewBg, Circle(), new Color(0.22f, 0.58f, 1f));
            Depth(previewBgImg, new Color(0.07f, 0.30f, 0.72f), 8f);
            var previewPic = NewRect("Preview", previewBg); Stretch(previewPic, 22f, 22f, 22f, 22f);
            var previewImg = AddImage(previewPic, null, Color.white); previewImg.raycastTarget = false; previewImg.preserveAspect = true;

            var inputRoot = NewRect("InputField", card);
            At(inputRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(740f, 112f));
            AddImage(inputRoot, Round(), Color.white, true, 0.6f);
            var inputRing = NewRect("Ring", inputRoot); Stretch(inputRing);
            AddImage(inputRing, Ring(), new Color(0.55f, 0.68f, 0.95f), true, 0.6f).raycastTarget = false;
            var area = NewRect("Text Area", inputRoot); Stretch(area, 28f, 10f, 28f, 10f);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = AddText(area, "Placeholder", "Enter name...", 52, new Color(0.5f, 0.55f, 0.65f), TextAlignmentOptions.Left);
            placeholder.fontStyle = FontStyles.Italic; Stretch(placeholder.rectTransform);
            var text = AddText(area, "Text", "", 52, Navy, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);
            var input = inputRoot.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = text; input.placeholder = placeholder;
            input.characterLimit = 12;

            var chooseLabel = AddText(card, "ChooseLabel", "Choose your picture", 46, SubText, TextAlignmentOptions.Center);
            At(chooseLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -575f), new Vector2(760f, 64f));

            // avatar grid: 5 columns x 2 rows of round picture buttons
            var avatarLibrary = AssetDatabase.LoadAssetAtPath<AvatarLibrary>(AvatarSetup.LibraryPath);
            int cells = 10;
            var buttons = new Button[cells]; var images = new Image[cells]; var marks = new GameObject[cells];
            for (int i = 0; i < cells; i++)
            {
                var cell = NewRect("Avatar" + i, card);
                float x = (i % 5 - 2) * 165f;
                float y = -650f - (i / 5) * 175f;
                At(cell, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(150f, 150f));
                var cellBg = AddImage(cell, Circle(), new Color(0.80f, 0.87f, 1f));
                buttons[i] = cell.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = cellBg;
                buttons[i].transition = Selectable.Transition.None;
                AddButtonFx(cell.gameObject, false);
                var pic = NewRect("Picture", cell); Stretch(pic, 12f, 12f, 12f, 12f);
                images[i] = AddImage(pic, avatarLibrary != null && i < avatarLibrary.avatars.Length ? avatarLibrary.avatars[i] : null, Color.white);
                images[i].raycastTarget = false; images[i].preserveAspect = true;
                var mark = NewRect("Selected", cell); Stretch(mark, -8f, -8f, -8f, -8f);
                AddImage(mark, Ring(), new Color(0.20f, 0.78f, 0.32f), true, 0.5f).raycastTarget = false;
                marks[i] = mark.gameObject;
            }

            // a photo from the phone's gallery (or none: back to an avatar)
            var gallery = MakeButton(card, "GalleryButton", "From Gallery", "Blue", new Vector2(520f, 110f), null);
            At((RectTransform)gallery.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-175f, -1030f), new Vector2(520f, 110f));
            var removePhoto = MakeButton(card, "RemovePhotoButton", "Remove", "Grey", new Vector2(300f, 110f), null);
            At((RectTransform)removePhoto.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(300f, -1030f), new Vector2(300f, 110f));
            // the phone owner's country: shown as a flag to other players online (never guessed: the player picks it)
            var countryRt = NewRect("CountryButton", card);
            At(countryRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1165f), new Vector2(820f, 110f));
            var countryBody = AddImage(countryRt, Round(), Color.white, true, 0.6f);
            Depth(countryBody, new Color(0.55f, 0.68f, 0.95f), 6f);
            var countryButton = countryRt.gameObject.AddComponent<Button>();
            countryButton.targetGraphic = countryBody;
            AddButtonFx(countryRt.gameObject, false);
            var globe = NewRect("Globe", countryRt);
            At(globe, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(64f, 64f));
            AddImage(globe, Ico("public"), new Color(0.22f, 0.45f, 0.9f)).raycastTarget = false;
            var countryFlag = FlagBadge(countryRt, "Flag", new Vector2(0f, 0.5f), new Vector2(60f, 0f), 76f);
            var countryLabel = AddText(countryRt, "Label", "Choose your country", 44, Navy, TextAlignmentOptions.Left);
            Stretch(countryLabel.rectTransform, 120f, 0f, 70f, 0f);
            countryLabel.enableAutoSizing = true; countryLabel.fontSizeMin = 26f; countryLabel.fontSizeMax = 44f;
            var chevron = NewRect("Chevron", countryRt);
            At(chevron, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(44f, 44f));
            AddImage(chevron, Load(KenneyUi + "Icons/arrow_basic_w.png"), SubText).raycastTarget = false;
            chevron.localRotation = Quaternion.Euler(0f, 0f, 180f);

            var hint = AddText(card, "PhotoHint", "", 34, new Color(0.85f, 0.2f, 0.2f), TextAlignmentOptions.Center);
            At(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1282f), new Vector2(820f, 50f));

            ok = MakeButton(card, "OkButton", "OK", "Green", new Vector2(340f, 130f), Icon("checkmark"));
            At((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(195f, 60f), new Vector2(340f, 130f));
            cancel = MakeButton(card, "CancelButton", "Cancel", "Grey", new Vector2(340f, 130f), null);
            At((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-195f, 60f), new Vector2(340f, 130f));

            editor = modal.gameObject.AddComponent<ProfileEditor>();
            var so = new SerializedObject(editor);
            so.FindProperty("panel").objectReferenceValue = modal.gameObject;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("preview").objectReferenceValue = previewImg;
            so.FindProperty("galleryButton").objectReferenceValue = gallery;
            so.FindProperty("removePhotoButton").objectReferenceValue = removePhoto;
            so.FindProperty("photoHint").objectReferenceValue = hint;
            so.FindProperty("countryButton").objectReferenceValue = countryButton;
            so.FindProperty("countryFlag").objectReferenceValue = countryFlag;
            so.FindProperty("countryLabel").objectReferenceValue = countryLabel;
            SetObjects(so.FindProperty("avatarButtons"), buttons);
            SetObjects(so.FindProperty("avatarImages"), images);
            SetObjects(so.FindProperty("avatarSelected"), marks);
            so.ApplyModifiedProperties();
            PopIn(modal, card);
            return modal.gameObject;
        }

        /// <summary>The country list pop-up: title, search box, scrolling list (flag, name, code), Close.</summary>
        static GameObject BuildCountryPicker(RectTransform root, out CountryPicker picker)
        {
            var modal = NewRect("CountryPickerModal", root); Stretch(modal);
            AddImage(modal, null, new Color(0f, 0f, 0.05f, 0.78f)).raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1640f));
            Depth(AddImage(card, Round(), Card, true, 0.45f), CardLip, 14f);
            var title = AddText(card, "Title", "Your Country", 66, Navy, TextAlignmentOptions.Center);
            At(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(760f, 90f));
            var note = AddText(card, "Note", "Other players see your flag in online games.", 34, SubText, TextAlignmentOptions.Center);
            At(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(840f, 50f));

            // search box
            var inputRoot = NewRect("Search", card);
            At(inputRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(840f, 104f));
            AddImage(inputRoot, Round(), Color.white, true, 0.6f);
            var ring = NewRect("Ring", inputRoot); Stretch(ring);
            AddImage(ring, Ring(), new Color(0.55f, 0.68f, 0.95f), true, 0.6f).raycastTarget = false;
            var lens = NewRect("Icon", inputRoot);
            At(lens, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(56f, 56f));
            AddImage(lens, Ico("search"), SubText).raycastTarget = false;
            var area = NewRect("Text Area", inputRoot); Stretch(area, 96f, 8f, 28f, 8f);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = AddText(area, "Placeholder", "Search country...", 46, new Color(0.5f, 0.55f, 0.65f), TextAlignmentOptions.Left);
            placeholder.fontStyle = FontStyles.Italic; Stretch(placeholder.rectTransform);
            var text = AddText(area, "Text", "", 46, Navy, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);
            var input = inputRoot.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = text; input.placeholder = placeholder;
            input.characterLimit = 40;

            // the list
            var scrollRt = NewRect("List", card);
            scrollRt.anchorMin = new Vector2(0f, 0f); scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(40f, 200f); scrollRt.offsetMax = new Vector2(-40f, -310f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            var viewport = NewRect("Viewport", scrollRt); Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, null, new Color(1f, 1f, 1f, 0.01f));
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 4, 20);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;

            var rowRt = NewRect("RowTemplate", content);
            rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 104f;
            var rowBody = AddImage(rowRt, Round(), Card, true, 0.6f);
            Depth(rowBody, CardLip, 6f);
            var rowButton = rowRt.gameObject.AddComponent<Button>();
            rowButton.targetGraphic = rowBody;
            rowButton.transition = Selectable.Transition.None;
            AddButtonFx(rowRt.gameObject, false);
            var rowFlagRt = NewRect("Flag", rowRt);
            At(rowFlagRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(84f, 58f));
            AddImage(rowFlagRt, null, Color.white).raycastTarget = false;
            var rowName = AddText(rowRt, "Name", "Country", 42, Navy, TextAlignmentOptions.Left);
            Stretch(rowName.rectTransform, 130f, 0f, 150f, 0f);
            rowName.enableAutoSizing = true; rowName.fontSizeMin = 24f; rowName.fontSizeMax = 42f;
            var rowCode = AddText(rowRt, "Code", "XX", 34, SubText, TextAlignmentOptions.Right);
            At(rowCode.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-84f, 0f), new Vector2(80f, 60f));
            var check = NewRect("Selected", rowRt);
            At(check, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(56f, 56f));
            AddImage(check, Icon("checkmark"), new Color(0.15f, 0.65f, 0.3f)).raycastTarget = false;

            var empty = AddText(card, "Empty", "No country matches.", 42, SubText, TextAlignmentOptions.Center);
            At(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(800f, 70f));
            empty.gameObject.SetActive(false);

            var close = MakeButton(card, "CloseButton", "Close", "Grey", new Vector2(420f, 130f), null);
            At((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(420f, 130f));

            picker = modal.gameObject.AddComponent<CountryPicker>();
            var so = new SerializedObject(picker);
            so.FindProperty("panel").objectReferenceValue = modal.gameObject;
            so.FindProperty("search").objectReferenceValue = input;
            so.FindProperty("scroll").objectReferenceValue = scroll;
            so.FindProperty("listRoot").objectReferenceValue = content;
            so.FindProperty("rowTemplate").objectReferenceValue = rowRt.gameObject;
            so.FindProperty("emptyText").objectReferenceValue = empty;
            so.FindProperty("normalColor").colorValue = Card;
            so.ApplyModifiedProperties();
            OnClick(close, picker.Close);
            rowRt.gameObject.SetActive(false);
            PopIn(modal, card);
            return modal.gameObject;
        }

        // ==================================================================================================
        //  GAME HUD (added to the existing Game scene)
        // ==================================================================================================

        [MenuItem("Ludo/Build Game HUD")]
        public static void BuildGameHud()
        {
            PrepareSprites();
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Camera cam = Camera.main;
            cam.backgroundColor = BackgroundBase;

            // remove what an earlier run (or the old world-space labels) left behind
            foreach (var n in new[] { "HudCanvas", "BackgroundCanvas", "EventSystem", "BoardBacking", "DiceTray" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }
            foreach (var n in new[] { "Game/TurnLabel", "Game/ToastLabel" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            // the frame round the board needs a little more room on the sides
            var fit = cam.GetComponent<CameraFit>();
            if (fit != null)
            {
                var fitObj = new SerializedObject(fit);
                fitObj.FindProperty("margin").floatValue = 1.0f;
                fitObj.ApplyModifiedProperties();
            }

            // background gradient (behind every world sprite)
            Canvas backCanvas = MakeCanvas(cam, "BackgroundCanvas", -100);
            Object.DestroyImmediate(backCanvas.GetComponent<GraphicRaycaster>());
            BuildBackground((RectTransform)backCanvas.transform, true);

            BuildBoardBacking();
            DiceSetup.Apply(Object.FindFirstObjectByType<DiceView>());      // the 3D dice (model, material, shadow)
            BuildDiceTray();

            Canvas canvas = MakeCanvas(cam, "HudCanvas", 5000);   // above every world sprite
            MakeEventSystem();
            RectTransform root = (RectTransform)canvas.transform;

            // the frame that follows the board, with a badge at each of its corners
            var frame = NewRect("BoardFrame", root);
            var boardFrame = frame.gameObject.AddComponent<BoardFrame>();
            var fo = new SerializedObject(boardFrame);
            fo.FindProperty("cam").objectReferenceValue = cam;
            fo.FindProperty("canvas").objectReferenceValue = canvas;
            fo.ApplyModifiedProperties();

            var badges = new PlayerBadge[4];
            badges[(int)Seat.Red] = MakeBadge(frame, "Badge_Red", new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 64f));
            badges[(int)Seat.Green] = MakeBadge(frame, "Badge_Green", new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 64f));
            badges[(int)Seat.Yellow] = MakeBadge(frame, "Badge_Yellow", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, -64f));
            badges[(int)Seat.Blue] = MakeBadge(frame, "Badge_Blue", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, -64f));

            var toast = BuildToast(frame);

            var safe = NewRect("SafeArea", root); Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // pause menu and result screen (hidden until needed)
            var hudGo = new GameObject("Hud");
            hudGo.transform.SetParent(root, false);
            var hud = hudGo.AddComponent<GameHud>();

            var pause = BuildPausePanel(root, hud);
            var result = BuildResultPanel(root, hud, out var resultTitle, out var resultAvatar, out var resultRing);

            var pauseButton = MakeRoundButton(safe, "PauseButton", "Grey", Icon("pause"), 124f);
            At((RectTransform)pauseButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -36f), new Vector2(124f, 124f));
            OnClick(pauseButton, hud.Pause);
            BuildGameOnlineExtras(safe, hud, pause, result);

            var banner = BuildTurnBanner(safe, out var turnText, out var turnAvatar, out var turnAvatarBg);
            var modeLine = AddText(safe, "ModeLine", "", 34, Color.white, TextAlignmentOptions.Center, true);
            At(modeLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1000f, 56f));
            modeLine.enableAutoSizing = true; modeLine.fontSizeMin = 22f; modeLine.fontSizeMax = 34f;
            modeLine.raycastTarget = false;
            modeLine.gameObject.SetActive(false);

            // the 3 - 2 - 1 - GO! that opens every match (hidden until the first turn is about to start)
            var countdown = AddText(safe, "Countdown", "", 260, Color.white, TextAlignmentOptions.Center, true);
            At(countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 320f));
            countdown.enableAutoSizing = true; countdown.fontSizeMin = 90f; countdown.fontSizeMax = 260f;
            countdown.raycastTarget = false;
            countdown.gameObject.SetActive(false);

            var controller = Object.FindFirstObjectByType<GameController>();
            var ho = new SerializedObject(hud);
            ho.FindProperty("countdownText").objectReferenceValue = countdown;
            ho.FindProperty("controller").objectReferenceValue = controller;
            SetObjects(ho.FindProperty("badges"), badges);
            ho.FindProperty("pausePanel").objectReferenceValue = pause;
            ho.FindProperty("resultPanel").objectReferenceValue = result;
            ho.FindProperty("resultTitle").objectReferenceValue = resultTitle;
            ho.FindProperty("resultAvatar").objectReferenceValue = resultAvatar;
            ho.FindProperty("resultRing").objectReferenceValue = resultRing;
            ho.FindProperty("resultFlag").objectReferenceValue = result.transform.Find("Stage/Podium/Flag/Picture").GetComponent<Image>();
            ho.FindProperty("toast").objectReferenceValue = toast;
            ho.FindProperty("turnText").objectReferenceValue = turnText;
            ho.FindProperty("turnAvatar").objectReferenceValue = turnAvatar;
            ho.FindProperty("turnAvatarBg").objectReferenceValue = turnAvatarBg;
            ho.FindProperty("modeText").objectReferenceValue = modeLine;
            BuildRollExtras(root, ho);
            ho.FindProperty("resultDetail").objectReferenceValue = result.transform.Find("Stage/Detail").GetComponent<TMP_Text>();
            ho.FindProperty("resultCard").objectReferenceValue = result.transform.Find("Stage/RewardCard").GetComponent<ResultCard>();
            ho.FindProperty("trophyRt").objectReferenceValue = (RectTransform)result.transform.Find("Stage/Trophy");
            ho.FindProperty("podiumRt").objectReferenceValue = (RectTransform)result.transform.Find("Stage/Podium");
            ho.FindProperty("titleRt").objectReferenceValue = (RectTransform)result.transform.Find("Stage/Title");
            ho.FindProperty("playAgainRt").objectReferenceValue = (RectTransform)result.transform.Find("Stage/PlayAgainButton");
            ho.FindProperty("menuRt").objectReferenceValue = (RectTransform)result.transform.Find("Stage/MainMenuButton");
            ho.FindProperty("menuLabel").objectReferenceValue = result.transform.Find("Stage/MainMenuButton/Label").GetComponent<TMP_Text>();
            ho.ApplyModifiedProperties();

            var co = new SerializedObject(controller);
            co.FindProperty("hud").objectReferenceValue = hud;
            co.ApplyModifiedProperties();

            pause.SetActive(false);
            result.SetActive(false);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[Ludo] Game HUD built.");
        }

        /// <summary>
        /// Ludo Star rule pieces of the game screen: a row of small dice for the numbers still to play, and the "which number
        /// first?" pop-up (up to 3 dice buttons) that appears over a pawn that can move with more than one of them.
        /// </summary>
        static void BuildRollExtras(RectTransform root, SerializedObject hud)
        {
            var faces = new Sprite[6];
            for (int i = 0; i < 6; i++) faces[i] = Load("Assets/ThirdParty/Kenney/BoardGame/Dice/dieWhite_border" + (i + 1) + ".png");

            var row = NewRect("PendingRolls", root);
            At(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 86f));
            var rowBg = AddImage(row, Round(), new Color(0.05f, 0.11f, 0.33f, 0.92f), true, 0.7f); rowBg.raycastTarget = false;
            var pending = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var d = NewRect("Die" + i, row);
                At(d, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 76f, 0f), new Vector2(64f, 64f));
                pending[i] = AddImage(d, faces[5], Color.white); pending[i].raycastTarget = false;
            }
            row.gameObject.SetActive(false);

            var chooser = NewRect("ValueChooser", root);
            At(chooser, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 200f));
            var cBg = AddImage(chooser, Round(), Card, true, 0.5f);
            Depth(cBg, CardLip, 8f);
            var ask = AddText(chooser, "Ask", "Move which number?", 34, Navy, TextAlignmentOptions.Center);
            At(ask.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(400f, 50f));
            var buttons = new Button[3]; var dice = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var b = NewRect("Value" + i, chooser);
                At(b, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 128f, 18f), new Vector2(112f, 112f));
                var bImg = AddImage(b, Round(), new Color(1f, 0.82f, 0.15f), true, 0.5f);
                Depth(bImg, new Color(0.7f, 0.45f, 0f), 6f);
                buttons[i] = b.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = bImg; buttons[i].transition = Selectable.Transition.None;
                AddButtonFx(b.gameObject, false);
                var d = NewRect("Die", b); Stretch(d, 14f, 14f, 14f, 14f);
                dice[i] = AddImage(d, faces[i], Color.white); dice[i].raycastTarget = false;
            }
            chooser.gameObject.SetActive(false);

            hud.FindProperty("pendingRow").objectReferenceValue = row;
            SetObjects(hud.FindProperty("pendingDice"), pending);
            hud.FindProperty("valueChooser").objectReferenceValue = chooser;
            SetObjects(hud.FindProperty("valueButtons"), buttons);
            SetObjects(hud.FindProperty("valueDice"), dice);
            var facesProp = hud.FindProperty("diceFaces");
            facesProp.arraySize = 6;
            for (int i = 0; i < 6; i++) facesProp.GetArrayElementAtIndex(i).objectReferenceValue = faces[i];
        }

        /// <summary>Soft shadow, light rim and dark backing behind the board, so it looks like a raised game table.</summary>
        static void BuildBoardBacking()
        {
            var root = new GameObject("BoardBacking");
            WorldPanel(root.transform, "Shadow", Load(Generated + "shadow_soft.png"), new Color(0f, 0f, 0.1f, 0.42f), 16.9f, new Vector2(0f, -0.22f), -4);
            WorldPanel(root.transform, "Rim", Load(Generated + "panel_world.png"), new Color(0.55f, 0.74f, 1f), 16.4f, Vector2.zero, -3);
            WorldPanel(root.transform, "Backing", Load(Generated + "panel_world.png"), new Color(0.06f, 0.13f, 0.38f), 16.05f, Vector2.zero, -2);
        }

        /// <summary>A dark rounded tray under the dice; it follows the dice (GameController moves the dice to fit the screen).</summary>
        static void BuildDiceTray()
        {
            var dice = Object.FindFirstObjectByType<DiceView>();
            var tray = new GameObject("DiceTray");
            var follow = tray.AddComponent<FollowPosition>();
            var fo = new SerializedObject(follow);
            fo.FindProperty("target").objectReferenceValue = dice.transform;
            fo.ApplyModifiedProperties();
            WorldPanel(tray.transform, "Shadow", Load(Generated + "shadow_soft.png"), new Color(0f, 0f, 0.1f, 0.35f), 3.35f, new Vector2(0f, -0.12f), 86);
            WorldPanel(tray.transform, "Rim", Load(Generated + "panel_world.png"), new Color(0.45f, 0.65f, 1f), 2.9f, Vector2.zero, 87);
            WorldPanel(tray.transform, "Body", Load(Generated + "panel_world.png"), new Color(0.05f, 0.11f, 0.33f), 2.7f, Vector2.zero, 88);
        }

        static void WorldPanel(Transform parent, string name, Sprite sprite, Color color, float size, Vector2 offset, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(size, size);
            sr.sortingOrder = order;
        }

        static RectTransform BuildTurnBanner(RectTransform safe, out TMP_Text text, out Image avatarPicture, out Image avatarBg)
        {
            var rt = NewRect("TurnBanner", safe);
            At(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(640f, 112f));
            var body = AddImage(rt, Round(), new Color(0.05f, 0.11f, 0.33f, 0.92f), true, 0.7f);
            body.raycastTarget = false;
            Depth(body, new Color(0.02f, 0.05f, 0.2f), 7f);
            var ring = NewRect("Ring", rt); Stretch(ring);
            AddImage(ring, Ring(), new Color(0.45f, 0.65f, 1f, 0.8f), true, 0.7f).raycastTarget = false;

            var avatar = NewRect("Avatar", rt);
            At(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(84f, 84f));
            avatarBg = AddImage(avatar, Circle(), Color.white);      // tinted with the current player's colour = the picture's border
            avatarBg.raycastTarget = false;
            var pictureBg = NewRect("PictureBg", avatar); Stretch(pictureBg, 5f, 5f, 5f, 5f);
            AddImage(pictureBg, Circle(), new Color(0.94f, 0.97f, 1f)).raycastTarget = false;
            var pictureRt = NewRect("Picture", pictureBg); Stretch(pictureRt, 4f, 4f, 4f, 4f);
            avatarPicture = AddImage(pictureRt, null, Color.white);
            avatarPicture.raycastTarget = false; avatarPicture.preserveAspect = true;

            var label = AddText(rt, "TurnText", "Player 1's turn", 52, Color.white, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform, 118f, 0f, 20f, 6f);
            label.enableAutoSizing = true; label.fontSizeMin = 28f; label.fontSizeMax = 52f;
            text = label;
            return rt;
        }

        static PlayerBadge MakeBadge(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            var rt = NewRect(name, parent);
            // 428px, not 470: the frame is exactly the board's own width, so two badges side by side used to leave only
            // ~13px between them (they read as overlapping, especially with a longer name) - this leaves a real ~95px gap
            At(rt, anchor, pivot, pos, new Vector2(428f, 138f));
            var bg = AddImage(rt, Round(), Color.white, true, 0.6f);
            bg.raycastTarget = false;
            Depth(bg, new Color(0f, 0f, 0f, 0.4f), 8f, 0.25f);
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            var gloss = NewRect("Gloss", rt);
            gloss.anchorMin = new Vector2(0f, 0.5f); gloss.anchorMax = Vector2.one;
            gloss.offsetMin = new Vector2(12f, 0f); gloss.offsetMax = new Vector2(-12f, -8f);
            AddImage(gloss, Round(), new Color(1f, 1f, 1f, 0.22f), true, 0.9f).raycastTarget = false;

            var glowRt = NewRect("Glow", rt); Stretch(glowRt, -12f, -12f, -12f, -12f);
            var glow = AddImage(glowRt, Ring(), Color.white, true, 0.45f);
            glow.raycastTarget = false;

            var avatar = NewRect("Avatar", rt);
            At(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(104f, 104f));
            var avatarImg = AddImage(avatar, Circle(), new Color(1f, 1f, 1f, 0.95f)); avatarImg.raycastTarget = false;
            Depth(avatarImg, new Color(0f, 0f, 0f, 0.25f), 4f, 0f);
            var pictureRt = NewRect("Picture", avatar); Stretch(pictureRt, 6f, 6f, 6f, 6f);
            var pictureImg = AddImage(pictureRt, null, Color.white); pictureImg.raycastTarget = false; pictureImg.preserveAspect = true;

            // voice chat: a green ring round the picture and a small microphone, shown while this player talks
            var speak = NewRect("Speaking", avatar); Stretch(speak, -12f, -12f, -12f, -12f);
            AddImage(speak, Ring(), SpeakGreen, true, 0.35f).raycastTarget = false;
            var micDot = NewRect("Mic", speak);
            At(micDot, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-6f, 10f), new Vector2(48f, 48f));
            var micBg = AddImage(micDot, Circle(), new Color(0.15f, 0.7f, 0.3f)); micBg.raycastTarget = false;
            Depth(micBg, new Color(0f, 0.25f, 0.08f), 3f, 0f);
            var micIcon = NewRect("Icon", micDot); Stretch(micIcon, 9f, 9f, 9f, 9f);
            AddImage(micIcon, Ico("mic"), Color.white).raycastTarget = false;
            speak.gameObject.SetActive(false);
            var flag = FlagBadge(avatar, "Flag", new Vector2(0f, 0f), new Vector2(4f, 8f), 58f);      // online: the player's country

            var nameText = AddText(rt, "Name", "Player", 46, Color.white, TextAlignmentOptions.Left);
            At(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(142f, -14f), new Vector2(263f, 66f));
            nameText.enableAutoSizing = true; nameText.fontSizeMin = 22f; nameText.fontSizeMax = 46f;
            nameText.overflowMode = TextOverflowModes.Ellipsis;      // a very long name is cut with ... rather than spilling out
            var sub = AddText(rt, "Subtitle", "", 30, Color.white, TextAlignmentOptions.Left);
            At(sub.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(142f, 14f), new Vector2(263f, 44f));
            sub.enableAutoSizing = true; sub.fontSizeMin = 20f; sub.fontSizeMax = 30f;
            sub.overflowMode = TextOverflowModes.Ellipsis;

            // the turn timer: a bar in the place of the subtitle, shown only for the player whose turn it is (online)
            var timerRt = NewRect("Timer", rt);
            At(timerRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(142f, 14f), new Vector2(263f, 40f));
            var timerBg = AddImage(timerRt, Round(), new Color(0.03f, 0.06f, 0.22f, 0.85f), true, 0.9f);
            timerBg.raycastTarget = false;
            var timerClip = NewRect("FillMask", timerRt); Stretch(timerClip, 4f, 4f, 4f, 4f);
            AddImage(timerClip, Round(), Color.white, true, 0.9f).raycastTarget = false;
            timerClip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var timerFillRt = NewRect("Fill", timerClip); Stretch(timerFillRt);
            var timerFill = AddImage(timerFillRt, Round(), new Color(0.30f, 0.88f, 0.40f), true, 0.9f);
            timerFill.raycastTarget = false;
            var timerLabel = AddText(timerRt, "Seconds", "20", 30, Color.white, TextAlignmentOptions.Center, true);
            Stretch(timerLabel.rectTransform);
            timerRt.gameObject.SetActive(false);

            var badge = rt.gameObject.AddComponent<PlayerBadge>();
            var so = new SerializedObject(badge);
            so.FindProperty("timerRoot").objectReferenceValue = timerRt.gameObject;
            so.FindProperty("timerFill").objectReferenceValue = timerFill;
            so.FindProperty("timerLabel").objectReferenceValue = timerLabel;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("subtitleText").objectReferenceValue = sub;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("avatarImage").objectReferenceValue = pictureImg;
            so.FindProperty("flagImage").objectReferenceValue = flag;
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("speaking").objectReferenceValue = speak.gameObject;
            so.ApplyModifiedProperties();
            return badge;
        }

        static GameObject BuildPausePanel(RectTransform root, GameHud hud)
        {
            var modal = NewRect("PausePanel", root); Stretch(modal);
            AddImage(modal, null, new Color(0f, 0f, 0.06f, 0.75f)).raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 930f));
            Depth(AddImage(card, Round(), DarkPanel, true, 0.45f), DarkPanelLip, 16f);
            var ring = NewRect("Ring", card); Stretch(ring);
            AddImage(ring, Ring(), new Color(0.45f, 0.65f, 1f, 0.55f), true, 0.45f).raycastTarget = false;

            var pauseIcon = NewRect("Icon", card);
            At(pauseIcon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(120f, 120f));
            AddImage(pauseIcon, Icon("pause"), Color.white).raycastTarget = false;
            var title = AddText(card, "Title", "Paused", 104, Color.white, TextAlignmentOptions.Center, true);
            At(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(700f, 140f));

            var resume = MakeButton(card, "ResumeButton", "Resume", "Green", new Vector2(640f, 155f), Icon2("icon_play_light"));
            At((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -370f), new Vector2(640f, 155f));
            var restart = MakeButton(card, "RestartButton", "Restart", "Blue", new Vector2(640f, 155f), Icon("return"));
            At((RectTransform)restart.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -555f), new Vector2(640f, 155f));
            var menu = MakeButton(card, "MainMenuButton", "Main Menu", "Purple", new Vector2(640f, 155f), Icon("home"));
            At((RectTransform)menu.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -740f), new Vector2(640f, 155f));
            OnClick(resume, hud.Resume);
            OnClick(restart, hud.RestartGame);
            OnClick(menu, hud.GoToMenu);
            PopIn(modal, card);
            return modal.gameObject;
        }

        /// <summary>
        /// The message banner (No Moves / Three Sixes / Captured), centred on the board. Its colours and words are set at
        /// run time by ToastBanner; the wide rounded shape, the gloss and the lip are built here.
        /// </summary>
        static ToastBanner BuildToast(RectTransform frame)
        {
            var holder = NewRect("Toast", frame); Stretch(holder);
            var group = holder.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;

            var panel = NewRect("Panel", holder);
            At(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 190f));
            var body = AddImage(panel, Round(), new Color(1f, 0.68f, 0.14f), true, 0.5f);
            body.raycastTarget = false;
            Depth(body, new Color(0.78f, 0.38f, 0.04f), 14f, 0.35f);
            var lip = panel.GetComponents<Shadow>()[0];             // the first Shadow is the darker lip; ToastBanner recolours it
            var ring = NewRect("Ring", panel); Stretch(ring);
            AddImage(ring, Ring(), new Color(1f, 1f, 1f, 0.55f), true, 0.5f).raycastTarget = false;
            var gloss = NewRect("Gloss", panel);
            gloss.anchorMin = new Vector2(0f, 0.55f); gloss.anchorMax = Vector2.one;
            gloss.offsetMin = new Vector2(24f, 0f); gloss.offsetMax = new Vector2(-24f, -14f);
            AddImage(gloss, Round(), new Color(1f, 1f, 1f, 0.24f), true, 0.9f).raycastTarget = false;

            // single icon in a round badge (No Moves, Captured)
            var badge = NewRect("IconBadge", panel);
            At(badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 4f), new Vector2(130f, 130f));
            var badgeImg = AddImage(badge, Circle(), new Color(0f, 0f, 0.15f, 0.28f)); badgeImg.raycastTarget = false;
            var iconRt = NewRect("Icon", badge);
            At(iconRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
            var iconImg = AddImage(iconRt, Icon("cross"), Color.white); iconImg.raycastTarget = false;
            Depth(iconImg, new Color(0f, 0f, 0.1f, 0.35f), 4f, 0f);

            // three dice showing 6 (Three Sixes)
            var sixes = NewRect("Sixes", panel);
            At(sixes, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 4f), new Vector2(230f, 130f));
            var six = Load("Assets/ThirdParty/Kenney/BoardGame/Dice/dieWhite_border6.png");
            for (int i = 0; i < 3; i++)
            {
                var die = NewRect("Die" + i, sixes);
                At(die, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 62f, (i == 1 ? 8f : 0f)), new Vector2(104f, 104f));
                die.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * -12f);
                var dieImg = AddImage(die, six, Color.white); dieImg.raycastTarget = false;
                Depth(dieImg, new Color(0f, 0f, 0.1f, 0.35f), 4f, 0f);
            }

            var title = AddText(panel, "Title", "No Moves", 78, Color.white, TextAlignmentOptions.Left, true);
            At(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(285f, -22f), new Vector2(570f, 100f));
            title.enableAutoSizing = true; title.fontSizeMin = 44f; title.fontSizeMax = 78f;
            var sub = AddText(panel, "Subtitle", "Turn passes on", 44, new Color(1f, 1f, 1f, 0.92f), TextAlignmentOptions.Left, true);
            At(sub.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(285f, 26f), new Vector2(570f, 62f));

            var banner = holder.gameObject.AddComponent<ToastBanner>();
            var so = new SerializedObject(banner);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("lip").objectReferenceValue = lip;
            so.FindProperty("icon").objectReferenceValue = iconImg;
            so.FindProperty("iconBadge").objectReferenceValue = badge.gameObject;
            so.FindProperty("sixes").objectReferenceValue = sixes.gameObject;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("subtitle").objectReferenceValue = sub;
            so.FindProperty("crossIcon").objectReferenceValue = Icon("cross");
            so.FindProperty("diceIcon").objectReferenceValue = Load("Assets/ThirdParty/Kenney/BoardGame/Dice/dieWhite_border6.png");
            so.FindProperty("captureIcon").objectReferenceValue = Icon("return");
            so.ApplyModifiedProperties();
            return banner;
        }

        static GameObject BuildResultPanel(RectTransform root, GameHud hud, out TMP_Text title, out Image avatarPicture, out Image ring)
        {
            var modal = NewRect("ResultPanel", root); Stretch(modal);
            AddImage(modal, null, new Color(0.02f, 0.05f, 0.2f, 0.93f)).raycastTarget = true;
            var stage = NewRect("Stage", modal); Stretch(stage);

            // confetti: small stars falling slowly (decoration only)
            var rng = new System.Random(11);
            Color[] confetti = { new Color(1f, 0.82f, 0.15f), new Color(0.3f, 0.85f, 0.4f), new Color(0.4f, 0.7f, 1f), new Color(1f, 0.4f, 0.45f), new Color(0.75f, 0.5f, 1f) };
            for (int i = 0; i < 34; i++)
            {
                var star = NewRect("Confetti" + i, stage);
                float size = 28f + (float)rng.NextDouble() * 34f;
                At(star, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((float)(rng.NextDouble() - 0.5) * 1100f, (float)(rng.NextDouble() - 0.5) * 2100f), new Vector2(size, size));
                star.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);
                AddImage(star, Icon2("star"), confetti[i % confetti.Length]).raycastTarget = false;
                AddDrift(star, new Vector2((float)(rng.NextDouble() - 0.5) * 30f, -(150f + (float)rng.NextDouble() * 220f)),
                    (float)(rng.NextDouble() - 0.5) * 120f, 25f + (float)rng.NextDouble() * 35f);
            }

            var rays = NewRect("Rays", stage);
            At(rays, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1700f, 1700f));
            AddImage(rays, Load(Generated + "rays.png"), new Color(1f, 0.85f, 0.3f, 0.34f)).raycastTarget = false;
            rays.gameObject.AddComponent<Spin>();
            var glow = NewRect("Glow", stage);
            At(glow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1000f, 1000f));
            AddImage(glow, Load(Generated + "glow_radial.png"), new Color(1f, 0.85f, 0.35f, 0.5f)).raycastTarget = false;

            var trophy = NewRect("Trophy", stage);
            At(trophy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 560f), new Vector2(300f, 300f));
            var trophyImg = AddImage(trophy, Icon("trophy"), Gold); trophyImg.raycastTarget = false;
            Depth(trophyImg, new Color(0.55f, 0.32f, 0f), 8f, 0.3f);
            trophy.gameObject.AddComponent<Bob>();

            var podium = NewRect("Podium", stage);
            At(podium, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(340f, 340f));
            ring = AddImage(podium, Circle(), Color.white); ring.raycastTarget = false;     // tinted with the winner's colour at run time
            Depth(ring, new Color(0f, 0f, 0.2f, 0.45f), 12f);
            var inner = NewRect("Inner", podium); Stretch(inner, 20f, 20f, 20f, 20f);
            AddImage(inner, Circle(), new Color(0.95f, 0.97f, 1f)).raycastTarget = false;
            var pictureRt = NewRect("WinnerPicture", inner); Stretch(pictureRt, 16f, 16f, 16f, 16f);
            avatarPicture = AddImage(pictureRt, null, Color.white);
            avatarPicture.raycastTarget = false; avatarPicture.preserveAspect = true;
            var resultFlag = FlagBadge(podium, "Flag", new Vector2(1f, 0f), new Vector2(-40f, 40f), 120f);   // online: the winner's country

            title = AddText(stage, "Title", "Player 1 Wins!", 100, Gold, TextAlignmentOptions.Center, true);
            At(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(940f, 140f));
            title.enableAutoSizing = true; title.fontSizeMin = 52f; title.fontSizeMax = 100f;

            // online: what the match changed for you ("Rating +14 ... Weekly Cup +3")
            var detail = AddText(stage, "Detail", "", 44, Color.white, TextAlignmentOptions.Center, true);
            detail.textWrappingMode = TextWrappingModes.Normal;
            At(detail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -285f), new Vector2(940f, 180f));
            detail.enableAutoSizing = true; detail.fontSizeMin = 24f; detail.fontSizeMax = 40f;
            detail.gameObject.SetActive(false);

            BuildRewardCard(stage, hud);

            var again = MakeButton(stage, "PlayAgainButton", "Play Again", "Green", new Vector2(700f, 160f), Icon("return"), true);
            At((RectTransform)again.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -460f), new Vector2(700f, 160f));
            var menu = MakeButton(stage, "MainMenuButton", "Main Menu", "Blue", new Vector2(700f, 150f), Icon("home"));
            At((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -640f), new Vector2(700f, 150f));
            OnClick(again, hud.RestartGame);
            OnClick(menu, hud.GoToMenu);
            PopIn(modal, stage);
            return modal.gameObject;
        }

        /// <summary>
        /// The online reward card of the result screen (hidden offline): Rank Points, XP, coins, rank, level, the RANK UP / LEVEL UP
        /// banner and the optional "watch an ad" button underneath. Positions are only a base: GameHud moves the rest around it.
        /// </summary>
        static void BuildRewardCard(RectTransform stage, GameHud hud)
        {
            var card = NewRect("RewardCard", stage);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(940f, 640f));
            Depth(AddImage(card, Round(), new Color(0.05f, 0.12f, 0.38f, 0.94f), true, 0.5f), new Color(0f, 0.03f, 0.18f, 0.9f), 12f);

            var header = AddText(card, "Header", "", 40, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Center);
            At(header.rectTransform, TopCenter, TopCenter, new Vector2(0f, -22f), new Vector2(860f, 96f));
            header.textWrappingMode = TextWrappingModes.Normal;
            header.enableAutoSizing = true; header.fontSizeMin = 26f; header.fontSizeMax = 40f;

            var rankRow = CardRow(card, "RankRow", "RANK POINTS", -125f, out var rankValue);
            var xpRow = CardRow(card, "XpRow", "XP", -205f, out var xpValue);
            var coinsRow = CardRow(card, "CoinsRow", "COINS", -285f, out var coinsValue);
            var tierRow = CardRow(card, "TierRow", "RANK", -365f, out var tierValue);
            var levelRow = CardRow(card, "LevelRow", "LEVEL", -445f, out var levelValue);

            var banner = AddText(card, "Banner", "", 58, Gold, TextAlignmentOptions.Center, true);
            At(banner.rectTransform, TopCenter, TopCenter, new Vector2(0f, -520f), new Vector2(880f, 110f));
            banner.textWrappingMode = TextWrappingModes.Normal;
            banner.enableAutoSizing = true; banner.fontSizeMin = 32f; banner.fontSizeMax = 58f;

            var ad = MakeButton(card, "AdButton", "WATCH AD", "Orange", new Vector2(860f, 170f), Ico("flash_on"), true);
            At((RectTransform)ad.transform, BottomCenter, BottomCenter, new Vector2(0f, -195f), new Vector2(860f, 170f));
            var adLabel = ad.transform.Find("Label").GetComponent<TMP_Text>();
            adLabel.textWrappingMode = TextWrappingModes.Normal;
            adLabel.enableAutoSizing = true; adLabel.fontSizeMin = 30f; adLabel.fontSizeMax = 46f;
            OnClick(ad, hud.WatchAd);

            var note = AddText(card, "AdNote", "", 40, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            At(note.rectTransform, BottomCenter, BottomCenter, new Vector2(0f, -150f), new Vector2(880f, 80f));
            note.enableAutoSizing = true; note.fontSizeMin = 26f; note.fontSizeMax = 40f;

            var rc = card.gameObject.AddComponent<ResultCard>();
            var so = new SerializedObject(rc);
            so.FindProperty("headerText").objectReferenceValue = header;
            so.FindProperty("rankRow").objectReferenceValue = rankRow;
            so.FindProperty("rankValue").objectReferenceValue = rankValue;
            so.FindProperty("xpRow").objectReferenceValue = xpRow;
            so.FindProperty("xpValue").objectReferenceValue = xpValue;
            so.FindProperty("coinsRow").objectReferenceValue = coinsRow;
            so.FindProperty("coinsValue").objectReferenceValue = coinsValue;
            so.FindProperty("tierRow").objectReferenceValue = tierRow;
            so.FindProperty("tierValue").objectReferenceValue = tierValue;
            so.FindProperty("levelRow").objectReferenceValue = levelRow;
            so.FindProperty("levelValue").objectReferenceValue = levelValue;
            so.FindProperty("bannerText").objectReferenceValue = banner;
            so.FindProperty("adButton").objectReferenceValue = ad.gameObject;
            so.FindProperty("adLabel").objectReferenceValue = adLabel;
            so.FindProperty("adNote").objectReferenceValue = note;
            so.ApplyModifiedProperties();
            card.gameObject.SetActive(false);
        }

        /// <summary>One "LABEL ........ value" line of the reward card.</summary>
        static GameObject CardRow(RectTransform card, string name, string label, float y, out TMP_Text value)
        {
            var row = NewRect(name, card);
            At(row, TopCenter, TopCenter, new Vector2(0f, y), new Vector2(880f, 76f));
            var l = AddText(row, "Label", label, 38, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.Left);
            At(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(330f, 70f));
            value = AddText(row, "Value", "", 50, Color.white, TextAlignmentOptions.Right, true);
            At(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(520f, 76f));
            value.enableAutoSizing = true; value.fontSizeMin = 28f; value.fontSizeMax = 50f;
            return row.gameObject;
        }

        // ==================================================================================================
        //  SMALL BUILDING BLOCKS
        // ==================================================================================================

        static void PrepareSprites()
        {
            if (!File.Exists(Generated + "panel_round.png") || !File.Exists(UiArtGenerator.OutlineMaterialPath))
                UiArtGenerator.Generate();
            if (!File.Exists(AvatarSetup.LibraryPath)) AvatarSetup.Run();
        }

        static Sprite Load(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogError("[Ludo] Missing sprite: " + path);
            return s;
        }

        static Sprite Icon(string name) => Load(KenneyUi + "Icons/game_" + name + ".png");
        static Sprite Icon2(string name) => Load(KenneyUi + "Icons/" + name + ".png");   // icons without the game_ prefix
        static Sprite Round() => Load(Generated + "panel_round.png");
        static Sprite Ring() => Load(Generated + "ring_round.png");
        static Sprite Circle() => Load(Generated + "circle.png");
        static Sprite Pawn() => Load("Assets/ThirdParty/Kenney/BoardGame/Pieces/pieceWhite_border00.png");
        static Sprite Robot() => Load(Generated + "ai_robot_white.png");   // white copy of the Material robot, so it can sit on a coloured badge
        static Sprite Ico(string name) => Load(Generated + name + "_white.png");   // white Google Material icons (mic, share, copy, group, ...)
        static Material OutlineMaterial() => AssetDatabase.LoadAssetAtPath<Material>(UiArtGenerator.OutlineMaterialPath);

        static Camera MakeCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundBase;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
            return cam;
        }

        static Canvas MakeCanvas(Camera cam, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;   // drawn by the camera, so it can be screenshotted and sorted with sprites
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);     // design size; other phones scale from it
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;                              // 0 = keep the width, tall phones just get more height
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void MakeEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>A full-screen menu screen: it fades and grows in when shown (ScreenFx).</summary>
        static RectTransform NewScreen(string name, RectTransform parent)
        {
            var rt = NewRect(name, parent);
            Stretch(rt);
            rt.gameObject.AddComponent<ScreenFx>();
            return rt;
        }

        /// <summary>Makes a pop-up fade in while its card grows.</summary>
        static void PopIn(RectTransform modal, RectTransform content)
        {
            var fx = modal.gameObject.AddComponent<ScreenFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("content").objectReferenceValue = content;
            so.ApplyModifiedProperties();
        }

        static void Stretch(RectTransform r, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Anchor to one point of the parent, then set the pivot, offset and size.</summary>
        static void At(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        static Image AddImage(RectTransform r, Sprite sprite, Color color, bool sliced = false, float ppuMultiplier = 1f)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sliced) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppuMultiplier; }
            return img;
        }

        /// <summary>
        /// The 3D look: a darker copy of the shape shifted down (the "lip") plus a soft, larger, fainter copy under it
        /// (the drop shadow). UnityEngine.UI.Shadow draws these copies behind the shape, so no extra objects are needed.
        /// </summary>
        static void Depth(Graphic g, Color lip, float lipHeight, float dropAlpha = 0.28f)
        {
            var l = g.gameObject.AddComponent<Shadow>();
            l.effectColor = lip; l.effectDistance = new Vector2(0f, -lipHeight); l.useGraphicAlpha = false;
            if (dropAlpha <= 0f) return;
            var d = g.gameObject.AddComponent<Shadow>();
            d.effectColor = new Color(0f, 0f, 0.1f, dropAlpha); d.effectDistance = new Vector2(0f, -lipHeight - 14f); d.useGraphicAlpha = false;
        }

        static TextMeshProUGUI AddText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align, bool outlined = false)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            if (outlined) t.fontSharedMaterial = OutlineMaterial();   // dark outline + soft shadow for text on coloured backgrounds
            return t;
        }

        static void AddDrift(RectTransform rt, Vector2 velocity, float spin, float sway)
        {
            var drift = rt.gameObject.AddComponent<Drift>();
            var so = new SerializedObject(drift);
            so.FindProperty("velocity").vector2Value = velocity;
            so.FindProperty("spin").floatValue = spin;
            so.FindProperty("sway").floatValue = sway;
            so.FindProperty("swaySpeed").floatValue = 0.6f + Mathf.Abs(spin) * 0.01f;
            so.ApplyModifiedProperties();
        }

        /// <summary>Layered background: deep blue, a lighter top, a darker bottom, a soft glow, and slowly drifting dice and pawns.</summary>
        static void BuildBackground(RectTransform root, bool decorations)
        {
            var bg = NewRect("Background", root); Stretch(bg);
            AddImage(bg, null, BackgroundBase).raycastTarget = false;

            var top = NewRect("LightTop", bg); Stretch(top);
            AddImage(top, Load(Generated + "gradient_v.png"), new Color(0.32f, 0.62f, 1f, 0.55f)).raycastTarget = false;
            var bottom = NewRect("DarkBottom", bg); Stretch(bottom);
            bottom.localScale = new Vector3(1f, -1f, 1f);   // flipped: dark at the bottom
            AddImage(bottom, Load(Generated + "gradient_v.png"), new Color(0.07f, 0.03f, 0.32f, 0.85f)).raycastTarget = false;

            var glow = NewRect("Glow", bg);
            At(glow, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 1500f));
            AddImage(glow, Load(Generated + "glow_radial.png"), new Color(0.45f, 0.75f, 1f, 0.5f)).raycastTarget = false;

            if (!decorations) return;
            var decor = NewRect("Decorations", bg); Stretch(decor);
            var rng = new System.Random(7);
            var sprites = new List<Sprite>();
            for (int i = 1; i <= 6; i++) sprites.Add(Load("Assets/ThirdParty/Kenney/BoardGame/Dice/dieWhite_border" + i + ".png"));
            for (int i = 0; i < 14; i++)
            {
                var item = NewRect("Floater" + i, decor);
                float size = 90f + (float)rng.NextDouble() * 110f;
                At(item, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((float)(rng.NextDouble() - 0.5) * 1100f, (float)(rng.NextDouble() - 0.5) * 2000f), new Vector2(size, size));
                item.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);
                var img = AddImage(item, sprites[i % 6], new Color(1f, 1f, 1f, 0.09f + (float)rng.NextDouble() * 0.07f));
                img.raycastTarget = false;
                AddDrift(item, new Vector2(0f, 16f + (float)rng.NextDouble() * 28f), ((float)rng.NextDouble() - 0.5f) * 30f, 20f);
            }
        }

        /// <summary>
        /// A chunky mobile-game button: rounded face in the style's colour, a darker lip underneath, a soft shadow,
        /// a glossy highlight on the upper half, an outlined label and an optional icon on the left.
        /// </summary>
        static Button MakeButton(Transform parent, string name, string label, string styleName, Vector2 size, Sprite icon, bool pulse = false)
        {
            var style = Style(styleName);
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;
            var body = AddImage(rt, Round(), style.face, true, size.y >= 140f ? 0.55f : 0.8f);
            Depth(body, style.lip, Mathf.Max(8f, size.y * 0.075f));

            var gloss = NewRect("Gloss", rt);
            gloss.anchorMin = new Vector2(0f, 0.52f); gloss.anchorMax = Vector2.one;
            gloss.offsetMin = new Vector2(size.y * 0.12f, 0f); gloss.offsetMax = new Vector2(-size.y * 0.12f, -size.y * 0.07f);
            AddImage(gloss, Round(), new Color(1f, 1f, 1f, style.darkLabel ? 0.55f : 0.26f), true, 0.9f).raycastTarget = false;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None;
            AddButtonFx(rt.gameObject, pulse);

            var text = AddText(rt, "Label", label, size.y * 0.42f, style.darkLabel ? Navy : Color.white, TextAlignmentOptions.Center, !style.darkLabel);
            // with an icon on the left, centre the label in the space that is left so the two never touch
            Stretch(text.rectTransform, icon != null ? size.y * 0.95f : 0f, 0f, icon != null ? size.y * 0.25f : 0f, size.y * 0.1f);
            if (icon != null)
            {
                var ic = NewRect("Icon", rt);
                float iconSize = size.y * (icon.name.StartsWith("game_") ? 0.72f : 0.5f);   // the game_ icons carry more empty margin
                At(ic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(size.y * 0.4f, size.y * 0.04f), new Vector2(iconSize, iconSize));
                var iconImg = AddImage(ic, icon, style.darkLabel ? Navy : Color.white);
                iconImg.raycastTarget = false;
                if (!style.darkLabel) Depth(iconImg, new Color(0f, 0f, 0.1f, 0.35f), 3f, 0f);
            }
            return button;
        }

        static void AddButtonFx(GameObject go, bool pulse, SfxId sound = SfxId.Click)
        {
            var fx = go.AddComponent<ButtonFx>();
            if (!pulse && sound == SfxId.Click) return;
            var so = new SerializedObject(fx);
            so.FindProperty("pulse").boolValue = pulse;
            so.FindProperty("sound").enumValueIndex = (int)sound;
            so.ApplyModifiedProperties();
        }

        static Button MakeRoundButton(Transform parent, string name, string styleName, Sprite icon, float size, SfxId sound = SfxId.Click)
        {
            var style = Style(styleName);
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            var body = AddImage(rt, Circle(), style.face);
            Depth(body, style.lip, size * 0.07f);
            var gloss = NewRect("Gloss", rt);
            At(gloss, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.16f), new Vector2(size * 0.72f, size * 0.5f));
            AddImage(gloss, Circle(), new Color(1f, 1f, 1f, style.darkLabel ? 0.6f : 0.28f)).raycastTarget = false;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None;
            AddButtonFx(rt.gameObject, false, sound);
            var ic = NewRect("Icon", rt);
            At(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.03f), new Vector2(size * 0.52f, size * 0.52f));
            AddImage(ic, icon, style.darkLabel ? Navy : Color.white).raycastTarget = false;
            return button;
        }

        /// <summary>A light rounded card with a coloured icon badge, a title and a subtitle. The whole card is a button.</summary>
        static Button CardButton(RectTransform parent, string name, float y, Sprite icon, string title, string subtitle, Color accent, bool chevron)
        {
            var rt = NewRect(name, parent);
            At(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(940f, 190f));
            var img = AddImage(rt, Round(), Card, true, 0.5f);
            Depth(img, CardLip, 10f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;
            var fx = rt.gameObject.AddComponent<ButtonFx>();
            var fxObj = new SerializedObject(fx);
            fxObj.FindProperty("pressedScale").floatValue = 0.97f;
            fxObj.ApplyModifiedProperties();

            var badge = NewRect("IconBadge", rt);
            At(badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(38f, 5f), new Vector2(130f, 130f));
            var badgeImg = AddImage(badge, Circle(), accent); badgeImg.raycastTarget = false;
            Depth(badgeImg, Color.Lerp(accent, Color.black, 0.45f), 7f, 0f);
            var shine = NewRect("Shine", badge);
            At(shine, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(96f, 60f));
            AddImage(shine, Circle(), new Color(1f, 1f, 1f, 0.25f)).raycastTarget = false;
            var ic = NewRect("Icon", badge);
            float iconSize = icon.name.EndsWith("_white") ? 84f : 108f;   // the multiplayer icon carries more empty margin; the white Material icons fill their square
            At(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(iconSize, iconSize));
            AddImage(ic, icon, Color.white).raycastTarget = false;

            var t = AddText(rt, "Title", title, 62, Navy, TextAlignmentOptions.Left);
            At(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -30f), new Vector2(560f, 80f));
            var s = AddText(rt, "Subtitle", subtitle, 38, SubText, TextAlignmentOptions.Left);
            At(s.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(200f, 28f), new Vector2(600f, 55f));

            if (chevron)
            {
                var arrow = NewRect("Chevron", rt);
                At(arrow, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-62f, 4f), new Vector2(54f, 54f));
                arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);   // the Kenney arrow points left; flip it to point right
                AddImage(arrow, Load(KenneyUi + "Icons/arrow_basic_w.png"), new Color(0.55f, 0.65f, 0.85f)).raycastTarget = false;
            }
            return button;
        }

        static Button SmallChoice(RectTransform parent, string name, string label, Vector2 position)
        {
            var rt = NewRect(name, parent);
            At(rt, new Vector2(0f, 1f), new Vector2(0f, 0.5f), position, new Vector2(160f, 110f));
            var img = AddImage(rt, Round(), Card, true, 0.75f);
            Depth(img, CardLip, 8f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;
            AddButtonFx(rt.gameObject, false);
            var t = AddText(rt, "Label", label, 64, Navy, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 0f, 0f, 0f, 6f);
            return button;
        }

        /// <summary>A round back arrow (top-left) and the screen title.</summary>
        static void Header(RectTransform screen, string title, ScreenRouter router)
        {
            var back = MakeRoundButton(screen, "BackButton", "Grey", Load(KenneyUi + "Icons/arrow_basic_w.png"), 124f, SfxId.Back);
            At((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -40f), new Vector2(124f, 124f));
            OnClick(back, router.Back);
            var t = AddText(screen, "Title", title, 88, Color.white, TextAlignmentOptions.Center, true);
            At(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(700f, 130f));
        }

        /// <summary>The game's logo: the app icon as a rounded tile with the "Ludo Fight" wordmark across its lower edge, gently floating.</summary>
        static void Logo(RectTransform parent, Vector2 anchor, Vector2 position, float size)
        {
            float tile = size * 0.6f;
            var root = NewRect("LogoRoot", parent);
            At(root, anchor, new Vector2(0.5f, 1f), position, new Vector2(size, tile + size * 0.2f));
            root.gameObject.AddComponent<Bob>();

            var shadow = NewRect("Shadow", root);
            At(shadow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -tile * 0.55f), new Vector2(tile * 1.6f, tile * 1.6f));
            AddImage(shadow, Load(Generated + "glow_radial.png"), new Color(0f, 0.02f, 0.2f, 0.5f)).raycastTarget = false;   // a radial fade has no visible edge

            var icon = NewRect("Icon", root);
            At(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(tile, tile));
            // the app icon with its rounded corners already drawn smoothly (Prototype/make_logo_icon.py)
            AddImage(icon, Load("Assets/_Game/Art/Branding/logo_icon.png"), Color.white).raycastTarget = false;

            var word = NewRect("Wordmark", root);
            At(word, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(size, size * 0.3f));
            var wordImg = AddImage(word, WelcomeSprite("welcome_wordmark.png"), Color.white);
            wordImg.raycastTarget = false; wordImg.preserveAspect = true;
        }

        /// <summary>
        /// A small country flag pinned to a picture's corner (hidden until a flag is set; the Image returned is the flag itself, on
        /// its own GameObject, so FlagLibrary.Apply can hide it). 'width' = flag width; the height follows the flag's shape.
        /// </summary>
        static Image FlagBadge(RectTransform parent, string name, Vector2 corner, Vector2 offset, float width)
        {
            FlagSetup.Ensure();
            var holder = NewRect(name, parent);
            At(holder, corner, new Vector2(0.5f, 0.5f), offset, new Vector2(width, width * 0.7f));
            var pic = NewRect("Picture", holder); Stretch(pic);
            var img = AddImage(pic, null, Color.white);
            img.raycastTarget = false; img.preserveAspect = true;
            var shadow = pic.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0.1f, 0.45f); shadow.effectDistance = new Vector2(2f, -3f);
            pic.gameObject.SetActive(false);
            return img;
        }

        /// <summary>
        /// The game-mode chips (Classic / Master / Arrow / Blitz / Team Up, like Ludo Star) at height 'y' (from the top), with a title and,
        /// if 'withRule', a line explaining the chosen mode. Every picker shares the saved choice (ModePicker.Current).
        /// </summary>
        static ModePicker BuildModePicker(RectTransform screen, float y, bool withRule, bool darkText = false)
        {
            var root = NewRect("ModePicker", screen);
            At(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(960f, withRule ? 230f : 110f));
            var picker = root.gameObject.AddComponent<ModePicker>();
            string[] names = { "Classic", "Master", "Arrow", "Blitz", "Team Up" };
            var chips = new Image[names.Length]; var labels = new TMP_Text[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var chip = NewRect("Mode" + names[i].Replace(" ", ""), root);
                At(chip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - (names.Length - 1) * 0.5f) * 192f, 0f), new Vector2(180f, 100f));
                chips[i] = AddImage(chip, Round(), new Color(0.93f, 0.96f, 1f), true, 0.5f);
                Depth(chips[i], CardLip, 6f);
                var button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = chips[i]; button.transition = Selectable.Transition.None;
                AddButtonFx(chip.gameObject, false);
                labels[i] = AddText(chip, "Label", names[i], 42, Navy, TextAlignmentOptions.Center, false);
                Stretch(labels[i].rectTransform, 6f, 0f, 6f, 6f);
                labels[i].enableAutoSizing = true; labels[i].fontSizeMin = 22f; labels[i].fontSizeMax = 38f;
                OnClickInt(button, picker.Choose, i);
            }
            TMP_Text rule = null;
            if (withRule)
            {
                rule = AddText(root, "Rule", "", 38, darkText ? SubText : new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Center, !darkText);
                rule.textWrappingMode = TextWrappingModes.Normal;
                rule.enableAutoSizing = true; rule.fontSizeMin = 26f; rule.fontSizeMax = 38f;
                At(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(940f, 100f));
            }
            var so = new SerializedObject(picker);
            SetObjects(so.FindProperty("chips"), chips);
            SetObjects(so.FindProperty("labels"), labels);
            so.FindProperty("ruleText").objectReferenceValue = rule;
            so.ApplyModifiedProperties();
            return picker;
        }

        static void SectionTitle(RectTransform parent, string text, float y)
        {
            var t = AddText(parent, "Section_" + text, text, 58, SubText, TextAlignmentOptions.Left);
            At(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, y), new Vector2(500f, 80f));
        }

        static Slider SliderRow(RectTransform parent, string label, Sprite icon, float y)
        {
            var row = NewRect(label + "Row", parent);
            At(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(940f, 110f));
            var ic = NewRect("Icon", row);
            At(ic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(80f, 80f));
            AddImage(ic, icon, Navy).raycastTarget = false;
            var t = AddText(row, "Label", label, 50, Navy, TextAlignmentOptions.Left);
            At(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(250f, 80f));

            var sliderRt = NewRect("Slider", row);
            At(sliderRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(490f, 68f));
            var track = NewRect("Background", sliderRt);
            track.anchorMin = new Vector2(0f, 0.3f); track.anchorMax = new Vector2(1f, 0.7f); track.offsetMin = track.offsetMax = Vector2.zero;
            var trackImg = AddImage(track, Round(), new Color(0.72f, 0.80f, 0.94f), true, 1f);
            Depth(trackImg, new Color(0.55f, 0.65f, 0.85f), 3f, 0f);
            var fillArea = NewRect("Fill Area", sliderRt);
            fillArea.anchorMin = new Vector2(0f, 0.3f); fillArea.anchorMax = new Vector2(1f, 0.7f); fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            var fill = NewRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = new Vector2(-4f, 0f); fill.offsetMax = new Vector2(4f, 0f);
            AddImage(fill, Round(), new Color(0.24f, 0.80f, 0.36f), true, 1f);
            var handleArea = NewRect("Handle Slide Area", sliderRt);
            Stretch(handleArea, 34f, 0f, 34f, 0f);
            var handle = NewRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(68f, 0f);   // y = 0: as tall as the slider (the slide area stretches it)
            var handleImg = AddImage(handle, Circle(), Color.white);
            Depth(handleImg, new Color(0.45f, 0.55f, 0.78f), 5f);
            var dot = NewRect("Dot", handle);
            At(dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            AddImage(dot, Circle(), new Color(0.20f, 0.56f, 1f)).raycastTarget = false;

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0.7f;
            return slider;
        }

        static Toggle ToggleRow(RectTransform parent, string label, Sprite icon, float y)
        {
            var row = NewRect(label + "Row", parent);
            At(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(940f, 110f));
            var ic = NewRect("Icon", row);
            At(ic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(80f, 80f));
            AddImage(ic, icon, Navy).raycastTarget = false;
            var t = AddText(row, "Label", label, 50, Navy, TextAlignmentOptions.Left);
            At(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(450f, 80f));

            // a modern on/off switch: pill track that changes colour, and a round knob that slides (SwitchToggle)
            var toggleRt = NewRect("Toggle", row);
            At(toggleRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(150f, 80f));
            var track = AddImage(toggleRt, Round(), new Color(0.25f, 0.8f, 0.35f), true, 0.8f);
            Depth(track, new Color(0.15f, 0.5f, 0.22f), 4f, 0f);
            var knob = NewRect("Knob", toggleRt);
            At(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(35f, 0f), new Vector2(64f, 64f));
            var knobImg = AddImage(knob, Circle(), Color.white); knobImg.raycastTarget = false;
            Depth(knobImg, new Color(0f, 0f, 0.15f, 0.3f), 3f, 0f);

            var toggle = toggleRt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = track;
            toggle.transition = Selectable.Transition.None;
            toggle.isOn = true;
            var sw = toggleRt.gameObject.AddComponent<SwitchToggle>();
            var so = new SerializedObject(sw);
            so.FindProperty("knob").objectReferenceValue = knob;
            so.FindProperty("track").objectReferenceValue = track;
            so.FindProperty("travel").floatValue = 35f;
            so.ApplyModifiedProperties();
            return toggle;
        }

        // ---------- wiring ----------

        static void OnClick(Button b, UnityAction action) => UnityEventTools.AddPersistentListener(b.onClick, action);
        static void OnClickInt(Button b, UnityAction<int> action, int value) => UnityEventTools.AddIntPersistentListener(b.onClick, action, value);

        static void SetObjects(SerializedProperty array, IList<GameObject> items)
        {
            array.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static void SetObjects<T>(SerializedProperty array, IList<T> items) where T : Component
        {
            array.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static void SetObjects(SerializedProperty array, IList<RectTransform> items)
        {
            array.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i].gameObject;
        }
    }
}
