using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;
using Ludo.Online;

namespace Ludo.EditorTools
{
    /// <summary>The online menu screens (Play Online, Room, Friends, invite pop-up). Same look as the rest of the menu.</summary>
    public static partial class UiSceneBuilder
    {
        static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        static readonly Color SpeakGreen = new Color(0.3f, 1f, 0.45f);

        // ==================================================================================================
        //  PLAY ONLINE (hub)
        // ==================================================================================================

        static RectTransform BuildOnline(RectTransform parent, RectTransform root, ScreenRouter router,
            DailyRewardPanel dailyPanel, ChestPanel chestPanel, DiceCollectionPanel dicePanel,
            out OnlineMenu menu, out GameObject joinModal, out GameObject busyOverlay)
        {
            var s = NewScreen("Screen_Online", parent);
            Header(s, "Play Online", router);
            menu = s.gameObject.AddComponent<OnlineMenu>();

            // the reference puts a gear opposite the back arrow; online, "settings" means your account
            var accountGear = MakeRoundButton(s, "AccountButton", "Grey", Icon("gear"), 124f);
            At((RectTransform)accountGear.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -40f), new Vector2(124f, 124f));
            OnClick(accountGear, menu.OpenAccount);

            // status line under the title (tap it to retry when it says the connection failed)
            var statusRt = NewRect("StatusButton", s);
            At(statusRt, TopCenter, TopCenter, new Vector2(0f, -172f), new Vector2(940f, 64f));
            var hit = AddImage(statusRt, null, new Color(0f, 0f, 0f, 0f)); hit.raycastTarget = true;
            var statusButton = statusRt.gameObject.AddComponent<Button>();
            statusButton.targetGraphic = hit; statusButton.transition = Selectable.Transition.None;
            var status = AddText(statusRt, "Status", "Connecting...", 44, Color.white, TextAlignmentOptions.Center, true);
            Stretch(status.rectTransform);
            status.enableAutoSizing = true; status.fontSizeMin = 26f; status.fontSizeMax = 44f;
            OnClick(statusButton, menu.Retry);

            // profile card: picture, name, rank, level + XP bar and coins; tap to open the profile and statistics
            var card0 = NewRect("ProfileCard", s);
            At(card0, TopCenter, TopCenter, new Vector2(0f, -238f), new Vector2(960f, 190f));
            var cardBody = AddImage(card0, Round(), DarkPanel, true, 0.5f);
            Depth(cardBody, DarkPanelLip, 10f);
            var cardButton = card0.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = cardBody; cardButton.transition = Selectable.Transition.None;
            AddButtonFx(card0.gameObject, false);
            var cardAv = NewRect("Avatar", card0);
            At(cardAv, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(112f, 112f));
            AddImage(cardAv, Circle(), new Color(0.80f, 0.87f, 1f)).raycastTarget = false;
            var cardPic = NewRect("Picture", cardAv); Stretch(cardPic, 8f, 8f, 8f, 8f);
            var cardPicImg = AddImage(cardPic, null, Color.white); cardPicImg.raycastTarget = false; cardPicImg.preserveAspect = true;
            var cardFlag = FlagBadge(cardAv, "Flag", new Vector2(1f, 0f), new Vector2(-22f, 10f), 50f);
            var cardName = AddText(card0, "Name", "Player 1", 52, Color.white, TextAlignmentOptions.Left, true);
            At(cardName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(156f, -16f), new Vector2(380f, 66f));
            cardName.enableAutoSizing = true; cardName.fontSizeMin = 28f; cardName.fontSizeMax = 52f;
            cardName.raycastTarget = false;

            // rank pill under the name
            var rankPill = NewRect("RankPill", card0);
            At(rankPill, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(156f, 26f), new Vector2(250f, 58f));
            AddImage(rankPill, Round(), new Color(0.55f, 0.34f, 0.10f), true, 1f).raycastTarget = false;
            var rankStar = NewRect("Star", rankPill);
            At(rankStar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(34f, 34f));
            AddImage(rankStar, Ikon("star"), Gold).raycastTarget = false;
            var cardRating = AddText(rankPill, "Value", "Bronze", 36, Gold, TextAlignmentOptions.Left);
            At(cardRating.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(190f, 52f));
            cardRating.enableAutoSizing = true; cardRating.fontSizeMin = 22f; cardRating.fontSizeMax = 36f;
            cardRating.raycastTarget = false;

            // level and the bar towards the next one
            var levelStar = NewRect("LevelStar", card0);
            At(levelStar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(432f, -26f), new Vector2(38f, 38f));
            AddImage(levelStar, Ikon("star"), Gold).raycastTarget = false;
            var cardLevel = AddText(card0, "Level", "Lv. 1", 38, Color.white, TextAlignmentOptions.Left, true);
            At(cardLevel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(478f, -18f), new Vector2(190f, 54f));
            cardLevel.enableAutoSizing = true; cardLevel.fontSizeMin = 24f; cardLevel.fontSizeMax = 38f;
            cardLevel.raycastTarget = false;
            var xpTrack = NewRect("XpTrack", card0);
            At(xpTrack, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(432f, -6f), new Vector2(236f, 24f));
            AddImage(xpTrack, Round(), new Color(0.02f, 0.06f, 0.20f, 0.85f), true, 3f).raycastTarget = false;
            var xpFillRt = NewRect("Fill", xpTrack); Stretch(xpFillRt, 3f, 3f, 3f, 3f);
            var cardXpBar = AddImage(xpFillRt, Round(), new Color(0.35f, 0.88f, 0.42f), true, 3.4f);
            cardXpBar.type = Image.Type.Filled; cardXpBar.fillMethod = Image.FillMethod.Horizontal;
            cardXpBar.fillOrigin = 0; cardXpBar.fillAmount = 0f; cardXpBar.raycastTarget = false;
            var cardXpText = AddText(card0, "XpText", "0 / 100", 28, new Color(0.62f, 0.96f, 0.68f), TextAlignmentOptions.Center);
            At(cardXpText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(432f, 24f), new Vector2(236f, 44f));
            cardXpText.enableAutoSizing = true; cardXpText.fontSizeMin = 18f; cardXpText.fontSizeMax = 28f;
            cardXpText.raycastTarget = false;

            // coins, with a plus that opens the free chest (where coins really come from)
            var coinIcon = NewRect("CoinIcon", card0);
            At(coinIcon, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-248f, 0f), new Vector2(52f, 52f));
            AddImage(coinIcon, Ikon("coin"), Gold).raycastTarget = false;
            var cardCoins = AddText(card0, "Coins", "0", 42, Color.white, TextAlignmentOptions.Left, true);
            At(cardCoins.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-92f, 0f), new Vector2(102f, 60f));
            cardCoins.enableAutoSizing = true; cardCoins.fontSizeMin = 24f; cardCoins.fontSizeMax = 42f;
            cardCoins.raycastTarget = false;
            var coinPlus = MakeRoundButton(card0, "CoinPlus", "Green", Icon("plus"), 58f);
            At((RectTransform)coinPlus.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(58f, 58f));
            OnClick(coinPlus, menu.OpenChest);
            OnClick(cardButton, menu.OpenProfile);

            // ---- "Select Mode" panel: table size, rules and entry fee, grouped as in the reference ----
            var modePanel = NewRect("ModePanel", s);
            At(modePanel, TopCenter, TopCenter, new Vector2(0f, -764f), new Vector2(960f, 330f));
            AddImage(modePanel, Round(), new Color(0.03f, 0.09f, 0.28f, 0.72f), true, 0.45f).raycastTarget = false;

            var sectionIcon = NewRect("SectionIcon", s);
            At(sectionIcon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -700f), new Vector2(44f, 44f));
            AddImage(sectionIcon, Ikon("sliders"), Color.white).raycastTarget = false;
            var sectionTitle = AddText(s, "SectionTitle", "Select Mode", 44, Color.white, TextAlignmentOptions.Left, true);
            At(sectionTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -692f), new Vector2(420f, 60f));
            sectionTitle.enableAutoSizing = true; sectionTitle.fontSizeMin = 28f; sectionTitle.fontSizeMax = 44f;

            // how many players at the table: 2, 3 or 4 (used by Ranked, Quick Match and Create Room)
            var chips = new Image[3]; var chipLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var chip = NewRect("Size" + (i + 2), modePanel);
                At(chip, TopCenter, TopCenter, new Vector2((i - 1) * 306f, -14f), new Vector2(290f, 92f));
                chips[i] = AddImage(chip, Round(), new Color(0.93f, 0.96f, 1f), true, 0.5f);
                var chipButton = chip.gameObject.AddComponent<Button>();
                chipButton.targetGraphic = chips[i]; chipButton.transition = Selectable.Transition.None;
                AddButtonFx(chip.gameObject, false);
                chipLabels[i] = AddText(chip, "Label", (i + 2) + " Players", 44, Navy, TextAlignmentOptions.Center, true);
                Stretch(chipLabels[i].rectTransform, 0f, 0f, 0f, 4f);
                chipLabels[i].enableAutoSizing = true; chipLabels[i].fontSizeMin = 28f; chipLabels[i].fontSizeMax = 44f;
                OnClickInt(chipButton, menu.SetSize, i + 2);
            }

            // the game mode for Quick Match and Create Room (Quick Match only pairs players who chose the same mode)
            BuildModePicker(modePanel, -118f, withRule: false);

            // the coin table for Quick Match (entry fee; the winner takes the pot; there is no free online table). Private rooms are free.
            var fees = Ludo.Core.CoinTables.OnlineFees;
            var feeChips = new Image[fees.Length]; var feeLabels = new TMP_Text[fees.Length];
            var feeTitle = AddText(modePanel, "EntryLabel", "Entry", 36, Color.white, TextAlignmentOptions.Left, true);
            At(feeTitle.rectTransform, TopCenter, TopCenter, new Vector2(-402f, -230f), new Vector2(150f, 80f));
            for (int i = 0; i < fees.Length; i++)
            {
                var chip = NewRect("Fee" + fees[i], modePanel);
                At(chip, TopCenter, TopCenter, new Vector2(-196f + i * 190f, -230f), new Vector2(176f, 80f));
                feeChips[i] = AddImage(chip, Round(), new Color(0.93f, 0.96f, 1f), true, 0.5f);
                Depth(feeChips[i], CardLip, 6f);
                var chipButton = chip.gameObject.AddComponent<Button>();
                chipButton.targetGraphic = feeChips[i]; chipButton.transition = Selectable.Transition.None;
                AddButtonFx(chip.gameObject, false);
                feeLabels[i] = AddText(chip, "Label", Ludo.Core.CoinTables.Label(fees[i]), 40, Navy, TextAlignmentOptions.Center, false);
                Stretch(feeLabels[i].rectTransform, 4f, 0f, 4f, 4f);
                feeLabels[i].enableAutoSizing = true; feeLabels[i].fontSizeMin = 24f; feeLabels[i].fontSizeMax = 40f;
                OnClickInt(chipButton, menu.SetFee, i);
            }

            // The reference has no reward buttons on this screen: the main menu carries Rewards and Chest on its rails,
            // with the same red dots. The daily pop-up still opens by itself here the first time each day.

            // the headline: one big banner, as in the reference
            var quick = HeadlineCard(s, "CardQuickMatch", -452f, Ico("flash_on"), "QUICK MATCH",
                "Ranked  \u00b7  Play with players worldwide",
                new Color(0.24f, 0.78f, 0.33f), new Color(0.08f, 0.48f, 0.16f),
                Color.white, new Color(0.86f, 1f, 0.88f), new Color(0.98f, 0.80f, 0.16f));
            OnClick(quick, menu.QuickMatch);

            // four wide cards, as in the reference. Leaderboards and Profile live on the bottom bar, the account on the
            // header gear and the dice collection on the main menu, so nothing became unreachable by trimming this grid.
            var create = WideCard(s, "CardCreateRoom", -238f, -1160f, Ico("group"), "Create Room", new Color(0.20f, 0.56f, 1f));
            var join = WideCard(s, "CardJoinRoom", 238f, -1160f, Ikon("key"), "Join Room", new Color(1f, 0.62f, 0.10f));
            var friends = WideCard(s, "CardFriends", -238f, -1330f, Icon("multiplayer"), "Friends", new Color(0.60f, 0.36f, 0.95f));
            var cup = WideCard(s, "CardTournaments", 238f, -1330f, Icon("trophy"), "Tournaments", new Color(0.93f, 0.20f, 0.36f));
            OnClick(create, menu.CreateRoom);
            OnClick(join, menu.OpenJoin);
            OnClickInt(friends, router.Show, Friends);
            OnClickInt(cup, router.Show, Tournaments);

            var shieldIc = NewRect("SafetyIcon", s);
            At(shieldIc, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-374f, 396f), new Vector2(32f, 32f));
            AddImage(shieldIc, Ikon("shield"), new Color(1f, 1f, 1f, 0.6f)).raycastTarget = false;
            var note = AddText(s, "SafetyNote", "Play fair  \u00b7  Mute, block or report players anytime.", 34, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.Center);
            At(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(22f, 382f), new Vector2(860f, 56f));
            note.enableAutoSizing = true; note.fontSizeMin = 22f; note.fontSizeMax = 34f;

            // what the chosen table costs and pays: "Entry 100 . winner takes 100 . 2 players"
            var entryInfo = AddText(s, "EntryInfo", "", 36, new Color(1f, 0.92f, 0.55f), TextAlignmentOptions.Center, true);
            At(entryInfo.rectTransform, TopCenter, TopCenter, new Vector2(0f, -1102f), new Vector2(940f, 50f));
            entryInfo.enableAutoSizing = true; entryInfo.fontSizeMin = 22f; entryInfo.fontSizeMax = 36f;

            AddSpread(s, new[] { (RectTransform)s.Find("CardQuickMatch"), (RectTransform)s.Find("SectionIcon"), (RectTransform)s.Find("SectionTitle"),
                    modePanel, entryInfo.rectTransform, (RectTransform)s.Find("CardCreateRoom"), (RectTransform)s.Find("CardJoinRoom"),
                    (RectTransform)s.Find("CardFriends"), (RectTransform)s.Find("CardTournaments") },
                new[] { 0.06f, 0.10f, 0.10f, 0.10f, 0.13f, 0.20f, 0.20f, 0.30f, 0.30f });

            NavBar(s, router, null, 0, Friends, Leaderboards, Profile);

            // ----- "Join with Code" pop-up -----
            var modal = NewRect("JoinModal", root); Stretch(modal);
            AddImage(modal, null, new Color(0f, 0f, 0.05f, 0.75f)).raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(880f, 640f));
            Depth(AddImage(card, Round(), Card, true, 0.45f), CardLip, 14f);
            var title = AddText(card, "Title", "Join a Room", 68, Navy, TextAlignmentOptions.Center);
            At(title.rectTransform, TopCenter, TopCenter, new Vector2(0f, -35f), new Vector2(760f, 95f));
            var sub = AddText(card, "Sub", "Type the 6-letter code from your friend", 38, SubText, TextAlignmentOptions.Center);
            At(sub.rectTransform, TopCenter, TopCenter, new Vector2(0f, -135f), new Vector2(820f, 55f));
            var input = MakeInput(card, "CodeInput", "CODE", new Vector2(0f, -215f), new Vector2(560f, 130f), 6, 84, TMP_InputField.ContentType.Alphanumeric);
            var error = AddText(card, "Error", "", 38, new Color(0.85f, 0.2f, 0.2f), TextAlignmentOptions.Center);
            At(error.rectTransform, TopCenter, TopCenter, new Vector2(0f, -365f), new Vector2(820f, 55f));
            error.enableAutoSizing = true; error.fontSizeMin = 24f; error.fontSizeMax = 38f;
            var joinButton = MakeButton(card, "JoinButton", "Join", "Green", new Vector2(340f, 130f), Icon("checkmark"));
            At((RectTransform)joinButton.transform, BottomCenter, BottomCenter, new Vector2(195f, 55f), new Vector2(340f, 130f));
            var cancelButton = MakeButton(card, "CancelButton", "Cancel", "Grey", new Vector2(340f, 130f), null);
            At((RectTransform)cancelButton.transform, BottomCenter, BottomCenter, new Vector2(-195f, 55f), new Vector2(340f, 130f));
            OnClick(joinButton, menu.ConfirmJoin);
            OnClick(cancelButton, menu.CloseJoin);
            PopIn(modal, card);
            joinModal = modal.gameObject;

            // ----- "Connecting..." veil -----
            var veil = NewRect("BusyOverlay", root); Stretch(veil);
            AddImage(veil, null, new Color(0f, 0f, 0.06f, 0.78f)).raycastTarget = true;
            var spinner = NewRect("Spinner", veil);
            At(spinner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(170f, 170f));
            AddImage(spinner, Icon2("star"), Gold).raycastTarget = false;
            spinner.gameObject.AddComponent<Spin>();
            var busyText = AddText(veil, "Text", "Connecting...", 60, Color.white, TextAlignmentOptions.Center, true);
            At(busyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(900f, 90f));
            busyOverlay = veil.gameObject;

            var so = new SerializedObject(menu);
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("roomScreen").intValue = Room;
            so.FindProperty("friendsScreen").intValue = Friends;
            so.FindProperty("accountScreen").intValue = Account;
            so.FindProperty("leaderboardScreen").intValue = Leaderboards;
            so.FindProperty("profileScreen").intValue = Profile;
            so.FindProperty("quickMatchScreen").intValue = QuickMatch;
            so.FindProperty("welcomeScreen").intValue = Welcome;
            so.FindProperty("profileName").objectReferenceValue = cardName;
            so.FindProperty("profileRating").objectReferenceValue = cardRating;
            so.FindProperty("profileLevel").objectReferenceValue = cardLevel;
            so.FindProperty("profileXpBar").objectReferenceValue = cardXpBar;
            so.FindProperty("profileXpText").objectReferenceValue = cardXpText;
            so.FindProperty("profileCoins").objectReferenceValue = cardCoins;
            so.FindProperty("profileAvatar").objectReferenceValue = cardPicImg;
            so.FindProperty("profileFlag").objectReferenceValue = cardFlag;
            so.FindProperty("entryInfo").objectReferenceValue = entryInfo;
            SetObjects(so.FindProperty("feeChips"), feeChips);
            SetObjects(so.FindProperty("feeLabels"), feeLabels);
            so.FindProperty("daily").objectReferenceValue = dailyPanel;
            so.FindProperty("diceCollection").objectReferenceValue = dicePanel;
            so.FindProperty("chest").objectReferenceValue = chestPanel;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("busyOverlay").objectReferenceValue = veil.gameObject;
            so.FindProperty("busyText").objectReferenceValue = busyText;
            so.FindProperty("joinModal").objectReferenceValue = modal.gameObject;
            so.FindProperty("codeInput").objectReferenceValue = input;
            so.FindProperty("joinError").objectReferenceValue = error;
            SetObjects(so.FindProperty("sizeChips"), chips);
            SetObjects(so.FindProperty("sizeLabels"), chipLabels);
            so.ApplyModifiedProperties();
            return s;
        }

        // ==================================================================================================
        //  ROOM (waiting room)
        // ==================================================================================================

        static RectTransform BuildRoom(RectTransform parent, RectTransform root, ScreenRouter router, out RoomScreen room, out GameObject moreModal)
        {
            var s = NewScreen("Screen_Room", parent);
            Header(s, "Room", router);
            room = s.gameObject.AddComponent<RoomScreen>();

            // ----- code card: only for rooms you share with friends -----
            var codeCard = NewRect("CodeCard", s);
            At(codeCard, TopCenter, TopCenter, new Vector2(0f, -190f), new Vector2(940f, 350f));
            Depth(AddImage(codeCard, Round(), DarkPanel, true, 0.45f), DarkPanelLip, 12f);
            var codeLabel = AddText(codeCard, "Label", "ROOM CODE", 36, new Color(0.7f, 0.82f, 1f), TextAlignmentOptions.Center);
            At(codeLabel.rectTransform, TopCenter, TopCenter, new Vector2(0f, -22f), new Vector2(600f, 50f));
            var code = AddText(codeCard, "Code", "------", 124, Gold, TextAlignmentOptions.Center, true);
            At(code.rectTransform, TopCenter, TopCenter, new Vector2(0f, -66f), new Vector2(880f, 160f));
            code.characterSpacing = 12f;
            var copy = MakeButton(codeCard, "CopyButton", "Copy", "Blue", new Vector2(285f, 100f), Ico("copy"));
            At((RectTransform)copy.transform, BottomCenter, BottomCenter, new Vector2(-300f, 30f), new Vector2(285f, 100f));
            var share = MakeButton(codeCard, "ShareButton", "Share", "Green", new Vector2(285f, 100f), Ico("share"));
            At((RectTransform)share.transform, BottomCenter, BottomCenter, new Vector2(0f, 30f), new Vector2(285f, 100f));
            var invite = MakeButton(codeCard, "InviteButton", "Invite", "Orange", new Vector2(285f, 100f), Ico("person_add"));
            At((RectTransform)invite.transform, BottomCenter, BottomCenter, new Vector2(300f, 30f), new Vector2(285f, 100f));
            OnClick(copy, room.Copy);
            OnClick(share, room.Share);
            OnClick(invite, room.InviteFriends);

            var hint = AddText(s, "Hint", "", 44, Color.white, TextAlignmentOptions.Center, true);
            At(hint.rectTransform, TopCenter, TopCenter, new Vector2(0f, -565f), new Vector2(940f, 60f));
            hint.enableAutoSizing = true; hint.fontSizeMin = 26f; hint.fontSizeMax = 44f;

            // ----- the four seats: a 2 x 2 grid of player cards, as in the reference lobby -----
            int[] seatOrder = Ludo.Core.Board.DefaultSeats(4);
            for (int i = 0; i < 4; i++)
            {
                var rowRt = NewRect("Seat" + (i + 1), s);
                At(rowRt, TopCenter, TopCenter, new Vector2((i % 2 == 0 ? -240f : 240f), -650f - (i / 2) * 360f), new Vector2(460f, 330f));
                var body = AddImage(rowRt, Round(), new Color(0.10f, 0.19f, 0.47f, 0.94f), true, 0.5f);
                Depth(body, DarkPanelLip, 10f);

                var glow = NewRect("Speaking", rowRt); Stretch(glow, -8f, -8f, -8f, -8f);
                AddImage(glow, Ring(), SpeakGreen, true, 0.5f).raycastTarget = false;

                // the seat's colour runs along the bottom edge of the card
                var colorBar = NewRect("SeatColor", rowRt);
                At(colorBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(400f, 16f));
                var barImg = AddImage(colorBar, Round(), SeatStyle.Colors[seatOrder[i]], true, 0.4f); barImg.raycastTarget = false;

                var av = NewRect("Avatar", rowRt);
                At(av, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(150f, 150f));
                AddImage(av, Circle(), new Color(0.80f, 0.87f, 1f)).raycastTarget = false;
                var ghost = NewRect("Silhouette", av); Stretch(ghost, 30f, 30f, 30f, 30f);
                AddImage(ghost, Ico("person"), new Color(0.35f, 0.47f, 0.75f, 0.9f)).raycastTarget = false;
                var pic = NewRect("Picture", av); Stretch(pic, 8f, 8f, 8f, 8f);
                var picImg = AddImage(pic, null, Color.white); picImg.raycastTarget = false; picImg.preserveAspect = true;
                var flagImg = FlagBadge(av, "Flag", new Vector2(1f, 0f), new Vector2(-14f, 6f), 54f);

                var nameText = AddText(rowRt, "Name", "Waiting...", 48, Color.white, TextAlignmentOptions.Center, true);
                At(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -184f), new Vector2(420f, 62f));
                nameText.enableAutoSizing = true; nameText.fontSizeMin = 26f; nameText.fontSizeMax = 48f;
                var tag = AddText(rowRt, "Tag", "", 34, Gold, TextAlignmentOptions.Center, true);
                At(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -248f), new Vector2(420f, 46f));
                tag.enableAutoSizing = true; tag.fontSizeMin = 22f; tag.fontSizeMax = 34f;

                var more = MakeRoundButton(rowRt, "MoreButton", "Grey", Ico("flag"), 72f);
                At((RectTransform)more.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(72f, 72f));
                OnClickInt(more, room.OpenMore, i);

                seatRefs[i] = new SeatRefs { root = rowRt.gameObject, avatar = picImg, flag = flagImg, seatColor = barImg, nameText = nameText, tagText = tag, glow = glow.gameObject, more = more };
            }

            var notice = AddText(s, "Notice", "", 40, Gold, TextAlignmentOptions.Center, true);
            At(notice.rectTransform, TopCenter, TopCenter, new Vector2(0f, -1390f), new Vector2(940f, 60f));
            notice.enableAutoSizing = true; notice.fontSizeMin = 24f; notice.fontSizeMax = 40f;

            var mic = MakeButton(s, "MicButton", "Voice chat", "Blue", new Vector2(460f, 130f), Ico("mic"));
            At((RectTransform)mic.transform, BottomCenter, BottomCenter, new Vector2(-240f, 300f), new Vector2(460f, 130f));
            OnClick(mic, room.ToggleVoice);
            var chatButton = MakeButton(s, "ChatButton", "Chat", "Orange", new Vector2(420f, 130f), Ico("chat"));
            At((RectTransform)chatButton.transform, BottomCenter, BottomCenter, new Vector2(260f, 300f), new Vector2(420f, 130f));
            var chatDot = BuildUnreadDot((RectTransform)chatButton.transform, out var chatDotText, new Vector2(-6f, -6f), 52f, true);
            BuildChat(root, chatButton, chatDot, chatDotText);

            var start = MakeButton(s, "StartButton", "Start Game", "Green", new Vector2(800f, 170f), Icon2("icon_play_light"), true);
            At((RectTransform)start.transform, BottomCenter, BottomCenter, new Vector2(0f, 100f), new Vector2(800f, 170f));
            OnClick(start, room.StartMatch);
            var ready = MakeButton(s, "ReadyButton", "I'm Ready", "Green", new Vector2(800f, 170f), Icon("checkmark"), true);
            At((RectTransform)ready.transform, BottomCenter, BottomCenter, new Vector2(0f, 100f), new Vector2(800f, 170f));
            OnClick(ready, room.ToggleReady);

            moreModal = BuildMoreModal(root, room, out var moreTitle);

            var so = new SerializedObject(room);
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("friendsScreen").intValue = Friends;
            so.FindProperty("codeText").objectReferenceValue = code;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("noticeText").objectReferenceValue = notice;
            so.FindProperty("startButton").objectReferenceValue = start;
            so.FindProperty("startLabel").objectReferenceValue = start.transform.Find("Label").GetComponent<TMP_Text>();
            so.FindProperty("readyButton").objectReferenceValue = ready;
            so.FindProperty("readyLabel").objectReferenceValue = ready.transform.Find("Label").GetComponent<TMP_Text>();
            so.FindProperty("privateOnly").objectReferenceValue = codeCard.gameObject;
            so.FindProperty("micIcon").objectReferenceValue = mic.transform.Find("Icon").GetComponent<Image>();
            so.FindProperty("micLabel").objectReferenceValue = mic.transform.Find("Label").GetComponent<TMP_Text>();
            so.FindProperty("micOn").objectReferenceValue = Ico("mic");
            so.FindProperty("micOff").objectReferenceValue = Ico("mic_off");
            so.FindProperty("moreModal").objectReferenceValue = moreModal;
            so.FindProperty("moreTitle").objectReferenceValue = moreTitle;
            var rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var e = rowsProp.GetArrayElementAtIndex(i);
                var r = seatRefs[i];
                e.FindPropertyRelative("root").objectReferenceValue = r.root;
                e.FindPropertyRelative("avatar").objectReferenceValue = r.avatar;
                e.FindPropertyRelative("flag").objectReferenceValue = r.flag;
                e.FindPropertyRelative("seatColor").objectReferenceValue = r.seatColor;
                e.FindPropertyRelative("nameText").objectReferenceValue = r.nameText;
                e.FindPropertyRelative("tagText").objectReferenceValue = r.tagText;
                e.FindPropertyRelative("speakingGlow").objectReferenceValue = r.glow;
                e.FindPropertyRelative("moreButton").objectReferenceValue = r.more;
            }
            so.ApplyModifiedProperties();
            return s;
        }

        struct SeatRefs
        {
            public GameObject root, glow;
            public Image avatar, flag, seatColor;
            public TMP_Text nameText, tagText;
            public Button more;
        }
        static readonly SeatRefs[] seatRefs = new SeatRefs[4];

        /// <summary>The pop-up for another player: mute (only for me), block, report.</summary>
        static GameObject BuildMoreModal(RectTransform root, RoomScreen room, out TMP_Text title)
        {
            var modal = NewRect("MoreModal", root); Stretch(modal);
            AddImage(modal, null, new Color(0f, 0f, 0.05f, 0.75f)).raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 1020f));
            Depth(AddImage(card, Round(), Card, true, 0.45f), CardLip, 14f);
            title = AddText(card, "Title", "Player", 66, Navy, TextAlignmentOptions.Center);
            At(title.rectTransform, TopCenter, TopCenter, new Vector2(0f, -35f), new Vector2(760f, 95f));
            title.enableAutoSizing = true; title.fontSizeMin = 34f; title.fontSizeMax = 66f;

            var addFriend = MakeButton(card, "AddFriendButton", "Add Friend", "Green", new Vector2(700f, 130f), Ico("person_add"));
            At((RectTransform)addFriend.transform, TopCenter, TopCenter, new Vector2(0f, -160f), new Vector2(700f, 130f));
            var mute = MakeButton(card, "MuteButton", "Mute / Unmute", "Blue", new Vector2(700f, 130f), Ico("mic_off"));
            At((RectTransform)mute.transform, TopCenter, TopCenter, new Vector2(0f, -310f), new Vector2(700f, 130f));
            var block = MakeButton(card, "BlockButton", "Block", "Orange", new Vector2(700f, 130f), Ico("block"));
            At((RectTransform)block.transform, TopCenter, TopCenter, new Vector2(0f, -460f), new Vector2(700f, 130f));
            var report = MakeButton(card, "ReportButton", "Report", "Red", new Vector2(700f, 130f), Ico("flag"));
            At((RectTransform)report.transform, TopCenter, TopCenter, new Vector2(0f, -610f), new Vector2(700f, 130f));
            OnClick(addFriend, room.AddFriendTarget);
            var close = MakeButton(card, "CloseButton", "Close", "Grey", new Vector2(700f, 130f), null);
            At((RectTransform)close.transform, BottomCenter, BottomCenter, new Vector2(0f, 45f), new Vector2(700f, 130f));
            OnClick(mute, room.MuteTarget);
            OnClick(block, room.BlockTarget);
            OnClick(report, room.ReportTarget);
            OnClick(close, room.CloseMore);
            PopIn(modal, card);
            return modal.gameObject;
        }

        // ==================================================================================================
        //  FRIENDS
        // ==================================================================================================

        static RectTransform BuildFriends(RectTransform parent, ScreenRouter router)
        {
            var s = NewScreen("Screen_Friends", parent);
            Header(s, "Friends", router);
            NavBar(s, router, null, 1, Friends, Leaderboards, Profile);
            var screen = s.gameObject.AddComponent<FriendsScreen>();

            // your own ID
            var idCard = NewRect("MyIdCard", s);
            At(idCard, TopCenter, TopCenter, new Vector2(0f, -190f), new Vector2(940f, 190f));
            Depth(AddImage(idCard, Round(), DarkPanel, true, 0.45f), DarkPanelLip, 10f);
            var idLabel = AddText(idCard, "Label", "YOUR FRIEND ID  (give it to friends)", 34, new Color(0.7f, 0.82f, 1f), TextAlignmentOptions.Left);
            At(idLabel.rectTransform, TopCenter, TopCenter, new Vector2(-70f, -18f), new Vector2(760f, 46f));
            var idText = AddText(idCard, "Id", "", 66, Gold, TextAlignmentOptions.Left, true);
            At(idText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 22f), new Vector2(620f, 90f));
            idText.enableAutoSizing = true; idText.fontSizeMin = 30f; idText.fontSizeMax = 66f;
            var copy = MakeButton(idCard, "CopyButton", "Copy", "Blue", new Vector2(230f, 96f), Ico("copy"));
            At((RectTransform)copy.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 26f), new Vector2(230f, 96f));
            OnClick(copy, screen.CopyMyId);

            // add a friend
            var addRow = NewRect("AddRow", s);
            At(addRow, TopCenter, TopCenter, new Vector2(0f, -410f), new Vector2(940f, 120f));
            var input = MakeInput(addRow, "AddInput", "Friend's ID (Name#1234)", new Vector2(-150f, 0f), new Vector2(640f, 112f), 40, 44, TMP_InputField.ContentType.Standard);
            var add = MakeButton(addRow, "AddButton", "Add", "Green", new Vector2(260f, 112f), Ico("person_add"));
            At((RectTransform)add.transform, TopCenter, TopCenter, new Vector2(340f, 0f), new Vector2(260f, 112f));
            OnClick(add, screen.AddFriend);

            // All / Online / Requests, as in the reference
            string[] friendTabs = { "All", "Online", "Requests" };
            var tabFaces = new Image[3]; var tabLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var tab = NewRect("Tab" + friendTabs[i], s);
                At(tab, TopCenter, TopCenter, new Vector2(-310f + i * 310f, -545f), new Vector2(295f, 88f));
                tabFaces[i] = AddImage(tab, Round(), new Color(0.90f, 0.94f, 1f), true, 0.8f);
                Depth(tabFaces[i], CardLip, 7f);
                var tb = tab.gameObject.AddComponent<Button>();
                tb.targetGraphic = tabFaces[i]; tb.transition = Selectable.Transition.None;
                AddButtonFx(tab.gameObject, false);
                tabLabels[i] = AddText(tab, "Label", friendTabs[i], 38, Navy, TextAlignmentOptions.Center);
                Stretch(tabLabels[i].rectTransform, 0f, 0f, 0f, 6f);
                tabLabels[i].enableAutoSizing = true; tabLabels[i].fontSizeMin = 24f; tabLabels[i].fontSizeMax = 38f;
                OnClickInt(tb, screen.SetTab, i);
            }

            var message = AddText(s, "Message", "", 40, Gold, TextAlignmentOptions.Center, true);
            At(message.rectTransform, TopCenter, TopCenter, new Vector2(0f, -645f), new Vector2(940f, 50f));
            message.enableAutoSizing = true; message.fontSizeMin = 24f; message.fontSizeMax = 40f;

            // scrolling list
            var scrollRt = NewRect("List", s);
            At(scrollRt, TopCenter, TopCenter, new Vector2(0f, -705f), new Vector2(940f, 1110f));
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            var viewport = NewRect("Viewport", scrollRt); Stretch(viewport);
            AddImage(viewport, null, new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 30);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;

            // templates (hidden; copied at run time)
            var header = AddText(content, "HeaderTemplate", "Friends", 46, Gold, TextAlignmentOptions.Left, true);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;
            header.margin = new Vector4(16f, 0f, 0f, 0f);

            var rowRt = NewRect("RowTemplate", content);
            rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 124f;
            Depth(AddImage(rowRt, Round(), Card, true, 0.5f), CardLip, 8f);
            var dot = NewRect("OnlineDot", rowRt);
            At(dot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(34f, 34f));
            var dotImg = AddImage(dot, Circle(), new Color(0.55f, 0.6f, 0.72f)); dotImg.raycastTarget = false;
            var rowName = AddText(rowRt, "Name", "Friend", 52, Navy, TextAlignmentOptions.Left);
            Stretch(rowName.rectTransform, 92f, 0f, 470f, 0f);
            rowName.enableAutoSizing = true; rowName.fontSizeMin = 28f; rowName.fontSizeMax = 52f;
            var btnB = MakeButton(rowRt, "ButtonB", "Remove", "Red", new Vector2(200f, 84f), null);
            At((RectTransform)btnB.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(200f, 84f));
            var btnA = MakeButton(rowRt, "ButtonA", "Invite", "Blue", new Vector2(200f, 84f), null);
            At((RectTransform)btnA.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-236f, 0f), new Vector2(200f, 84f));
            var view = rowRt.gameObject.AddComponent<FriendRowView>();
            var vo = new SerializedObject(view);
            vo.FindProperty("dot").objectReferenceValue = dotImg;
            vo.FindProperty("nameText").objectReferenceValue = rowName;
            vo.FindProperty("buttonA").objectReferenceValue = btnA;
            vo.FindProperty("labelA").objectReferenceValue = btnA.transform.Find("Label").GetComponent<TMP_Text>();
            vo.FindProperty("colorA").objectReferenceValue = btnA.GetComponent<Image>();
            vo.FindProperty("buttonB").objectReferenceValue = btnB;
            vo.FindProperty("labelB").objectReferenceValue = btnB.transform.Find("Label").GetComponent<TMP_Text>();
            vo.FindProperty("colorB").objectReferenceValue = btnB.GetComponent<Image>();
            vo.ApplyModifiedProperties();

            var empty = AddText(scrollRt, "Empty", "No friends yet.\nGive your ID to a friend, or add theirs above.", 44, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
            empty.textWrappingMode = TextWrappingModes.Normal;
            At(empty.rectTransform, TopCenter, TopCenter, new Vector2(0f, -240f), new Vector2(860f, 200f));

            var so = new SerializedObject(screen);
            SetObjects(so.FindProperty("tabFaces"), tabFaces);
            so.FindProperty("myIdText").objectReferenceValue = idText;
            so.FindProperty("addInput").objectReferenceValue = input;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("listRoot").objectReferenceValue = content;
            so.FindProperty("rowTemplate").objectReferenceValue = view;
            so.FindProperty("headerTemplate").objectReferenceValue = header;
            so.FindProperty("emptyText").objectReferenceValue = empty;
            so.ApplyModifiedProperties();
            rowRt.gameObject.SetActive(false);
            header.gameObject.SetActive(false);
            return s;
        }

        // ==================================================================================================
        //  INVITE POP-UP
        // ==================================================================================================

        static GameObject BuildInviteModal(RectTransform root, ScreenRouter router)
        {
            var modal = NewRect("InviteModal", root); Stretch(modal);
            AddImage(modal, null, new Color(0f, 0f, 0.05f, 0.7f)).raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 720f));
            Depth(AddImage(card, Round(), DarkPanel, true, 0.45f), DarkPanelLip, 16f);
            AddImage(NewRect("Ring", card), Ring(), new Color(0.45f, 0.65f, 1f, 0.55f), true, 0.45f).raycastTarget = false;
            Stretch((RectTransform)card.Find("Ring"));

            var avBg = NewRect("AvatarBg", card);
            At(avBg, TopCenter, TopCenter, new Vector2(0f, -50f), new Vector2(200f, 200f));
            var bgImg = AddImage(avBg, Circle(), new Color(0.22f, 0.58f, 1f));
            Depth(bgImg, new Color(0.07f, 0.30f, 0.72f), 8f);
            var pic = NewRect("Avatar", avBg); Stretch(pic, 18f, 18f, 18f, 18f);
            var picImg = AddImage(pic, null, Color.white); picImg.raycastTarget = false; picImg.preserveAspect = true;

            var msg = AddText(card, "Message", "A friend invited you to play!", 54, Color.white, TextAlignmentOptions.Center, true);
            msg.textWrappingMode = TextWrappingModes.Normal;
            At(msg.rectTransform, TopCenter, TopCenter, new Vector2(0f, -280f), new Vector2(780f, 150f));
            msg.enableAutoSizing = true; msg.fontSizeMin = 32f; msg.fontSizeMax = 54f;

            var join = MakeButton(card, "JoinButton", "Join", "Green", new Vector2(340f, 140f), Icon("checkmark"));
            At((RectTransform)join.transform, BottomCenter, BottomCenter, new Vector2(195f, 60f), new Vector2(340f, 140f));
            var later = MakeButton(card, "LaterButton", "Later", "Grey", new Vector2(340f, 140f), null);
            At((RectTransform)later.transform, BottomCenter, BottomCenter, new Vector2(-195f, 60f), new Vector2(340f, 140f));

            var listener = modal.gameObject.AddComponent<InviteListener>();
            OnClick(join, listener.Join);
            OnClick(later, listener.Later);
            var so = new SerializedObject(listener);
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("roomScreen").intValue = Room;
            so.FindProperty("modal").objectReferenceValue = modal.gameObject;
            so.FindProperty("messageText").objectReferenceValue = msg;
            so.FindProperty("avatar").objectReferenceValue = picImg;
            so.ApplyModifiedProperties();
            PopIn(modal, card);
            return modal.gameObject;
        }

        // ==================================================================================================
        //  LOGIN / SIGN UP PAGE
        // ==================================================================================================

        static RectTransform BuildAccount(RectTransform parent, ScreenRouter router, out AccountScreen account)
        {
            var s = NewScreen("Screen_Account", parent);
            Header(s, "Account", router);
            account = s.gameObject.AddComponent<AccountScreen>();

            var headline = AddText(s, "Headline", "", 50, Color.white, TextAlignmentOptions.Center, true);
            headline.textWrappingMode = TextWrappingModes.Normal;
            At(headline.rectTransform, TopCenter, TopCenter, new Vector2(0f, -230f), new Vector2(940f, 200f));
            headline.enableAutoSizing = true; headline.fontSizeMin = 30f; headline.fontSizeMax = 50f;

            // one column: guests see Google / Facebook (keeps their progress); everybody sees Log Out
            var column = NewRect("Buttons", s);
            At(column, TopCenter, TopCenter, new Vector2(0f, -500f), new Vector2(820f, 560f));
            var stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childAlignment = TextAnchor.UpperCenter;
            stack.spacing = 30f;
            stack.childControlWidth = stack.childControlHeight = false;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;

            var google = MakeButton(column, "GoogleButton", "Continue with Google", "Grey", new Vector2(800f, 140f), null);
            BrandIcon((RectTransform)google.transform, WelcomeSprite("icon_google.png"), 74f);
            var facebook = MakeButton(column, "FacebookButton", "Continue with Facebook", "Blue", new Vector2(800f, 140f), null);
            BrandIcon((RectTransform)facebook.transform, WelcomeSprite("icon_facebook.png"), 74f);
            var logout = MakeButton(column, "LogOutButton", "Log Out", "Red", new Vector2(800f, 140f), null);
            foreach (RectTransform child in column) child.sizeDelta = new Vector2(800f, 140f);
            OnClick(logout, account.LogOut);
            OnClick(google, account.Google);
            OnClick(facebook, account.Facebook);
            facebook.gameObject.SetActive(FacebookLogin.Enabled);        // not in this version (kept for the next one)

            var message = AddText(s, "Message", "", 42, Color.white, TextAlignmentOptions.Center, true);
            message.textWrappingMode = TextWrappingModes.Normal;
            At(message.rectTransform, TopCenter, TopCenter, new Vector2(0f, -1100f), new Vector2(940f, 150f));
            message.enableAutoSizing = true; message.fontSizeMin = 26f; message.fontSizeMax = 42f;

            var note = AddText(s, "Note", "A guest profile lives only on this phone. Log in with " + (FacebookLogin.Enabled ? "Google or Facebook" : "Google") + " to keep your progress on any phone.", 34,
                new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            note.textWrappingMode = TextWrappingModes.Normal;
            At(note.rectTransform, BottomCenter, BottomCenter, new Vector2(0f, 110f), new Vector2(900f, 110f));

            var so = new SerializedObject(account);
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("onlineScreen").intValue = Online;
            so.FindProperty("welcomeScreen").intValue = Welcome;
            so.FindProperty("headline").objectReferenceValue = headline;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("guestNote").objectReferenceValue = note.gameObject;
            so.FindProperty("logoutButton").objectReferenceValue = logout;
            so.FindProperty("googleButton").objectReferenceValue = google;
            so.FindProperty("facebookButton").objectReferenceValue = facebook;
            so.ApplyModifiedProperties();
            return s;
        }

        // ==================================================================================================
        //  TURN TIMER (game screen)
        // ==================================================================================================

        /// <summary>A bar under the turn banner that empties while an online player thinks (hidden in offline games).</summary>
        static TurnTimerBar BuildTurnTimer(RectTransform safe)
        {
            var root = NewRect("TurnTimer", safe);
            At(root, TopCenter, TopCenter, new Vector2(0f, -172f), new Vector2(520f, 44f));
            var bg = AddImage(root, Round(), new Color(0.03f, 0.07f, 0.24f, 0.9f), true, 0.9f);
            bg.raycastTarget = false;
            Depth(bg, new Color(0.02f, 0.04f, 0.15f), 4f, 0f);
            var clip = NewRect("FillMask", root); Stretch(clip, 5f, 5f, 5f, 5f);
            AddImage(clip, Round(), Color.white, true, 0.9f).raycastTarget = false;
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var fillRt = NewRect("Fill", clip); Stretch(fillRt);
            var fill = AddImage(fillRt, null, new Color(0.30f, 0.88f, 0.40f));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 1f; fill.raycastTarget = false;
            var label = AddText(root, "Seconds", "20", 34, Color.white, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform);

            var bar = root.gameObject.AddComponent<TurnTimerBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("root").objectReferenceValue = root.gameObject;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedProperties();
            root.gameObject.SetActive(false);
            return bar;
        }

        // ==================================================================================================
        //  SHARED HELPERS
        // ==================================================================================================

        /// <summary>A half-width menu tile: a coloured icon badge above a title. The whole tile is a button.</summary>
        /// <summary>The daily reward pop-up: 7 day tiles (coins), Claim, Watch ad x2, a status line, Close.</summary>
        static GameObject BuildDailyReward(RectTransform root, out DailyRewardPanel panel)
        {
            var modal = NewRect("DailyRewardModal", root); Stretch(modal);
            AddImage(modal, null, new Color(0f, 0f, 0.05f, 0.78f)).raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 1340f));
            Depth(AddImage(card, Round(), Card, true, 0.45f), CardLip, 14f);
            var title = AddText(card, "Title", "Daily Reward", 70, Navy, TextAlignmentOptions.Center);
            At(title.rectTransform, TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(800f, 95f));
            var sub = AddText(card, "Sub", "Come back every day for bigger gifts!", 36, SubText, TextAlignmentOptions.Center);
            At(sub.rectTransform, TopCenter, TopCenter, new Vector2(0f, -122f), new Vector2(880f, 50f));

            var tiles = new Image[7]; var amounts = new TMP_Text[7]; var ticks = new GameObject[7];
            for (int i = 0; i < 7; i++)
            {
                bool big = i == 6;
                var t = NewRect("Day" + (i + 1), card);
                float x = big ? 0f : (i % 3 - 1) * 290f;
                float y = big ? -770f : -200f - (i / 3) * 285f;
                At(t, TopCenter, TopCenter, new Vector2(x, y), new Vector2(big ? 860f : 265f, 260f));
                tiles[i] = AddImage(t, Round(), new Color(0.93f, 0.96f, 1f), true, 0.5f);
                Depth(tiles[i], CardLip, 8f);
                var day = AddText(t, "Day", "Day " + (i + 1), 38, SubText, TextAlignmentOptions.Center);
                At(day.rectTransform, TopCenter, TopCenter, new Vector2(0f, -12f), new Vector2(240f, 50f));
                var coin = NewRect("Coin", t);                                        // a gold coin: rim, face, shine
                At(coin, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(big ? 110f : 88f, big ? 110f : 88f));
                var rim = AddImage(coin, Circle(), new Color(0.85f, 0.55f, 0.05f)); rim.raycastTarget = false;
                var face = NewRect("Face", coin); Stretch(face, 9f, 9f, 9f, 9f);
                AddImage(face, Circle(), new Color(1f, 0.82f, 0.18f)).raycastTarget = false;
                var shine = NewRect("Shine", face);
                At(shine, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-8f, 10f), new Vector2(28f, 20f));
                AddImage(shine, Circle(), new Color(1f, 1f, 1f, 0.55f)).raycastTarget = false;
                amounts[i] = AddText(t, "Amount", "100", 46, Navy, TextAlignmentOptions.Center, false);
                At(amounts[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(240f, 56f));
                var tick = NewRect("Claimed", t);
                At(tick, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-28f, -28f), new Vector2(64f, 64f));
                var tickBg = AddImage(tick, Circle(), new Color(0.20f, 0.78f, 0.32f)); tickBg.raycastTarget = false;
                var tickIcon = NewRect("Icon", tick); Stretch(tickIcon, 12f, 12f, 12f, 12f);
                AddImage(tickIcon, Icon("checkmark"), Color.white).raycastTarget = false;
                tick.gameObject.SetActive(false);
                ticks[i] = tick.gameObject;
            }

            var status = AddText(card, "Status", "", 40, new Color(0.15f, 0.55f, 0.25f), TextAlignmentOptions.Center);
            At(status.rectTransform, TopCenter, TopCenter, new Vector2(0f, -1045f), new Vector2(880f, 56f));
            status.enableAutoSizing = true; status.fontSizeMin = 24f; status.fontSizeMax = 40f;

            var claim = MakeButton(card, "ClaimButton", "Claim", "Green", new Vector2(360f, 130f), Icon("checkmark"));
            At((RectTransform)claim.transform, BottomCenter, BottomCenter, new Vector2(-200f, 50f), new Vector2(360f, 130f));
            var ad = MakeButton(card, "AdButton", "Watch Ad x2", "Orange", new Vector2(400f, 130f), Icon2("icon_play_light"));
            At((RectTransform)ad.transform, BottomCenter, BottomCenter, new Vector2(210f, 50f), new Vector2(400f, 130f));
            var close = MakeRoundButton(card, "CloseButton", "Red", Icon("cross"), 96f);
            At((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -30f), new Vector2(96f, 96f));

            panel = modal.gameObject.AddComponent<DailyRewardPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panel").objectReferenceValue = modal.gameObject;
            SetObjects(so.FindProperty("dayTiles"), tiles);
            SetObjects(so.FindProperty("dayAmounts"), amounts);
            SetObjects(so.FindProperty("dayTicks"), ticks);
            so.FindProperty("claimButton").objectReferenceValue = claim;
            so.FindProperty("adButton").objectReferenceValue = ad;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedProperties();
            OnClick(claim, panel.Claim);
            OnClick(ad, panel.WatchAd);
            OnClick(close, panel.Close);
            PopIn(modal, card);
            modal.gameObject.SetActive(false);
            return modal.gameObject;
        }



        /// <summary>
        /// The free chest pop-up: a drawn treasure chest, the countdown to the next one, Open and an optional "Watch ad x2".
        /// The chest art is built from the same rounded shapes as the rest of the UI - no outside artwork.
        /// </summary>
        static GameObject BuildChest(RectTransform root, out ChestPanel panel)
        {
            var modal = NewRect("ChestModal", root); Stretch(modal);
            AddImage(modal, null, new Color(0f, 0f, 0.05f, 0.78f)).raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 1000f));
            Depth(AddImage(card, Round(), Card, true, 0.45f), CardLip, 14f);
            var title = AddText(card, "Title", "Free Chest", 70, Navy, TextAlignmentOptions.Center);
            At(title.rectTransform, TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(800f, 95f));
            var sub = AddText(card, "Sub", "A free pile of coins every " + Ludo.Core.Chest.IntervalHours + " hours. Always free.", 36, SubText, TextAlignmentOptions.Center);
            At(sub.rectTransform, TopCenter, TopCenter, new Vector2(0f, -122f), new Vector2(860f, 50f));
            sub.enableAutoSizing = true; sub.fontSizeMin = 24f; sub.fontSizeMax = 36f;

            // the chest itself: a lid, a body, a gold band and a lock, all from the generated rounded shapes
            var art = NewRect("ChestArt", card);
            At(art, TopCenter, TopCenter, new Vector2(0f, -210f), new Vector2(360f, 320f));
            var glow = NewRect("Glow", art);
            At(glow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(420f, 420f));
            AddImage(glow, Load(UiArtGenerator.Folder + "glow_radial.png"), new Color(1f, 0.85f, 0.35f, 0.5f)).raycastTarget = false;
            var body = NewRect("Body", art);
            At(body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(300f, 170f));
            var bodyImg = AddImage(body, Round(), new Color(0.55f, 0.32f, 0.16f), true, 0.35f); bodyImg.raycastTarget = false;
            Depth(bodyImg, new Color(0.34f, 0.19f, 0.08f), 10f);
            var lid = NewRect("Lid", art);
            At(lid, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 185f), new Vector2(320f, 120f));
            var lidImg = AddImage(lid, Round(), new Color(0.66f, 0.40f, 0.20f), true, 0.5f); lidImg.raycastTarget = false;
            Depth(lidImg, new Color(0.40f, 0.23f, 0.10f), 10f);
            var band = NewRect("Band", art);
            At(band, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 125f), new Vector2(340f, 34f));
            AddImage(band, Round(), new Color(1f, 0.82f, 0.18f), true, 0.9f).raycastTarget = false;
            var latch = NewRect("Latch", art);
            At(latch, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(70f, 70f));
            AddImage(latch, Circle(), new Color(1f, 0.74f, 0.10f)).raycastTarget = false;
            var latchHole = NewRect("Hole", latch);
            At(latchHole, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            AddImage(latchHole, Circle(), new Color(0.35f, 0.22f, 0.05f)).raycastTarget = false;

            var timer = AddText(card, "Timer", "Ready!", 62, Navy, TextAlignmentOptions.Center, false);
            At(timer.rectTransform, TopCenter, TopCenter, new Vector2(0f, -560f), new Vector2(760f, 90f));
            timer.enableAutoSizing = true; timer.fontSizeMin = 34f; timer.fontSizeMax = 62f;

            var status = AddText(card, "Status", "", 38, new Color(0.15f, 0.55f, 0.25f), TextAlignmentOptions.Center);
            At(status.rectTransform, TopCenter, TopCenter, new Vector2(0f, -660f), new Vector2(840f, 120f));
            status.textWrappingMode = TextWrappingModes.Normal;
            status.enableAutoSizing = true; status.fontSizeMin = 24f; status.fontSizeMax = 38f;

            var open = MakeButton(card, "OpenButton", "Open", "Green", new Vector2(340f, 130f), Icon("checkmark"));
            At((RectTransform)open.transform, BottomCenter, BottomCenter, new Vector2(-190f, 50f), new Vector2(340f, 130f));
            var openLabel = open.transform.Find("Label").GetComponent<TMP_Text>();
            var ad = MakeButton(card, "AdButton", "Watch Ad x2", "Orange", new Vector2(400f, 130f), null);   // no icon: the label already fills the button
            At((RectTransform)ad.transform, BottomCenter, BottomCenter, new Vector2(200f, 50f), new Vector2(400f, 130f));
            var close = MakeRoundButton(card, "CloseButton", "Red", Icon("cross"), 96f);
            At((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -30f), new Vector2(96f, 96f));

            panel = modal.gameObject.AddComponent<ChestPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panel").objectReferenceValue = modal.gameObject;
            so.FindProperty("chestArt").objectReferenceValue = art;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("openButton").objectReferenceValue = open;
            so.FindProperty("openLabel").objectReferenceValue = openLabel;
            so.FindProperty("adButton").objectReferenceValue = ad;
            so.ApplyModifiedProperties();
            OnClick(open, panel.Claim);
            OnClick(ad, panel.WatchAd);
            OnClick(close, panel.Close);
            PopIn(modal, card);
            modal.gameObject.SetActive(false);
            return modal.gameObject;
        }

        /// <summary>
        /// The dice collection pop-up: a tile per design in Ludo.Core.DiceSkins showing that design's real "5" face, its
        /// name and what it takes to get it. Tapping an owned design wears it; tapping one for sale buys it with coins.
        /// </summary>
        static GameObject BuildDiceCollection(RectTransform root, out DiceCollectionPanel panel)
        {
            var all = Ludo.Core.DiceSkins.All;
            var modal = NewRect("DiceCollectionModal", root); Stretch(modal);
            AddImage(modal, null, new Color(0f, 0f, 0.05f, 0.78f)).raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 1140f));
            Depth(AddImage(card, Round(), Card, true, 0.45f), CardLip, 14f);
            var title = AddText(card, "Title", "My Dice", 70, Navy, TextAlignmentOptions.Center);
            At(title.rectTransform, TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(800f, 95f));
            var sub = AddText(card, "Sub", "Every design rolls exactly the same - they are just prettier.", 34, SubText, TextAlignmentOptions.Center);
            At(sub.rectTransform, TopCenter, TopCenter, new Vector2(0f, -122f), new Vector2(900f, 50f));
            sub.enableAutoSizing = true; sub.fontSizeMin = 24f; sub.fontSizeMax = 34f;

            // the player's coin purse, so a price on a locked design means something
            var purse = NewRect("Purse", card);
            At(purse, TopCenter, TopCenter, new Vector2(0f, -178f), new Vector2(340f, 70f));
            AddImage(purse, Round(), new Color(0.97f, 0.93f, 0.78f), true, 0.5f).raycastTarget = false;
            var purseCoin = NewRect("Coin", purse);
            At(purseCoin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(46f, 46f));
            AddImage(purseCoin, Circle(), new Color(1f, 0.82f, 0.18f)).raycastTarget = false;
            var coins = AddText(purse, "Coins", "0", 42, Navy, TextAlignmentOptions.Left, false);
            Stretch(coins.rectTransform, 76f, 4f, 16f, 4f);

            var tiles = new Image[all.Length];
            var previews = new RawImage[all.Length];
            var names = new TMP_Text[all.Length];
            var notes = new TMP_Text[all.Length];
            var locks = new GameObject[all.Length];
            var buttons = new Button[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                var t = NewRect("Skin" + i, card);
                At(t, TopCenter, TopCenter, new Vector2((i % 3 - 1) * 296f, -255f - (i / 3) * 330f), new Vector2(284f, 310f));
                tiles[i] = AddImage(t, Round(), new Color(0.93f, 0.96f, 1f), true, 0.5f);
                Depth(tiles[i], CardLip, 8f);
                var b = t.gameObject.AddComponent<Button>();
                b.targetGraphic = tiles[i]; b.transition = Selectable.Transition.None;
                AddButtonFx(t.gameObject, false);
                buttons[i] = b;

                var face = NewRect("Face", t);                                  // the design's own picture, cut from its atlas
                At(face, TopCenter, TopCenter, new Vector2(0f, -18f), new Vector2(150f, 150f));
                previews[i] = face.gameObject.AddComponent<RawImage>();
                previews[i].raycastTarget = false;

                var padlock = NewRect("Lock", t);
                At(padlock, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(56f, 56f));
                AddImage(padlock, Circle(), new Color(0.35f, 0.40f, 0.52f, 0.92f)).raycastTarget = false;
                var padIcon = NewRect("Icon", padlock); Stretch(padIcon, 12f, 12f, 12f, 12f);
                AddImage(padIcon, Icon("cross"), Color.white).raycastTarget = false;
                locks[i] = padlock.gameObject;

                names[i] = AddText(t, "Name", all[i].Name, 42, Navy, TextAlignmentOptions.Center, false);
                At(names[i].rectTransform, TopCenter, TopCenter, new Vector2(0f, -178f), new Vector2(264f, 56f));
                names[i].enableAutoSizing = true; names[i].fontSizeMin = 26f; names[i].fontSizeMax = 42f;
                notes[i] = AddText(t, "Note", "", 32, SubText, TextAlignmentOptions.Center, false);
                At(notes[i].rectTransform, TopCenter, TopCenter, new Vector2(0f, -232f), new Vector2(264f, 64f));
                notes[i].textWrappingMode = TextWrappingModes.Normal;
                notes[i].enableAutoSizing = true; notes[i].fontSizeMin = 20f; notes[i].fontSizeMax = 32f;
            }

            var status = AddText(card, "Status", "", 38, new Color(0.15f, 0.55f, 0.25f), TextAlignmentOptions.Center);
            At(status.rectTransform, BottomCenter, BottomCenter, new Vector2(0f, 40f), new Vector2(880f, 120f));
            status.textWrappingMode = TextWrappingModes.Normal;
            status.enableAutoSizing = true; status.fontSizeMin = 24f; status.fontSizeMax = 38f;
            var close = MakeRoundButton(card, "CloseButton", "Red", Icon("cross"), 96f);
            At((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -30f), new Vector2(96f, 96f));

            panel = modal.gameObject.AddComponent<DiceCollectionPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panel").objectReferenceValue = modal.gameObject;
            so.FindProperty("grid").objectReferenceValue = card;
            SetObjects(so.FindProperty("tiles"), tiles);
            SetObjects(so.FindProperty("previews"), previews);
            SetObjects(so.FindProperty("names"), names);
            SetObjects(so.FindProperty("notes"), notes);
            SetObjects(so.FindProperty("locks"), locks);
            so.FindProperty("coinsText").objectReferenceValue = coins;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedProperties();
            for (int i = 0; i < buttons.Length; i++) OnClickInt(buttons[i], panel.Choose, i);
            OnClick(close, panel.Close);
            PopIn(modal, card);
            modal.gameObject.SetActive(false);
            return modal.gameObject;
        }

        static Button TileButton(RectTransform parent, string name, float x, float y, Sprite icon, string title, Color accent)
        {
            var rt = NewRect(name, parent);
            At(rt, TopCenter, TopCenter, new Vector2(x, y), new Vector2(455f, 170f));
            var img = AddImage(rt, Round(), Card, true, 0.5f);
            Depth(img, CardLip, 9f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img; button.transition = Selectable.Transition.None;
            AddButtonFx(rt.gameObject, false);
            var badge = NewRect("IconBadge", rt);
            At(badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 4f), new Vector2(112f, 112f));
            var badgeImg = AddImage(badge, Circle(), accent); badgeImg.raycastTarget = false;
            Depth(badgeImg, Color.Lerp(accent, Color.black, 0.45f), 6f, 0f);
            var ic = NewRect("Icon", badge);
            float size = icon.name.EndsWith("_white") ? 70f : icon.name == "star" ? 66f : 90f;
            At(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(size, size));
            AddImage(ic, icon, Color.white).raycastTarget = false;
            var t = AddText(rt, "Title", title, 46, Navy, TextAlignmentOptions.Left);
            Stretch(t.rectTransform, 150f, 0f, 16f, 6f);
            t.enableAutoSizing = true; t.fontSizeMin = 26f; t.fontSizeMax = 46f;
            return button;
        }

        // ==================================================================================================
        //  LEADERBOARDS  (Ranked / Weekly Cup / Friends)
        // ==================================================================================================

        static RectTransform BuildLeaderboards(RectTransform parent, ScreenRouter router)
        {
            var s = NewScreen("Screen_Leaderboards", parent);
            Header(s, "Leaderboards", router);
            NavBar(s, router, null, 2, Friends, Leaderboards, Profile);
            var screen = s.gameObject.AddComponent<LeaderboardScreen>();

            // three tabs
            string[] tabNames = { "Ranked", "Weekly Cup", "Friends" };
            var tabButtons = new Button[3]; var tabFaces = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var tab = NewRect("Tab" + tabNames[i].Replace(" ", ""), s);
                At(tab, TopCenter, TopCenter, new Vector2(-310f + i * 310f, -190f), new Vector2(295f, 100f));
                tabFaces[i] = AddImage(tab, Round(), new Color(0.90f, 0.94f, 1f), true, 0.8f);
                Depth(tabFaces[i], CardLip, 7f);
                tabButtons[i] = tab.gameObject.AddComponent<Button>();
                tabButtons[i].targetGraphic = tabFaces[i]; tabButtons[i].transition = Selectable.Transition.None;
                AddButtonFx(tab.gameObject, false);
                var label = AddText(tab, "Label", tabNames[i], 40, Navy, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 0f, 0f, 0f, 6f);
                label.enableAutoSizing = true; label.fontSizeMin = 24f; label.fontSizeMax = 40f;
            }

            var info = AddText(s, "Info", "", 36, Color.white, TextAlignmentOptions.Center, true);
            info.textWrappingMode = TextWrappingModes.Normal;
            At(info.rectTransform, TopCenter, TopCenter, new Vector2(0f, -310f), new Vector2(940f, 100f));
            info.enableAutoSizing = true; info.fontSizeMin = 24f; info.fontSizeMax = 36f;

            var mine = AddText(s, "MyLine", "", 44, Gold, TextAlignmentOptions.Center, true);
            At(mine.rectTransform, TopCenter, TopCenter, new Vector2(0f, -420f), new Vector2(940f, 64f));

            var scrollRt = NewRect("List", s);
            At(scrollRt, TopCenter, TopCenter, new Vector2(0f, -500f), new Vector2(940f, 1330f));
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            var viewport = NewRect("Viewport", scrollRt); Stretch(viewport);
            AddImage(viewport, null, new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 30);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;

            var rowRt = NewRect("RowTemplate", content);
            rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;
            var rowBody = AddImage(rowRt, Round(), Card, true, 0.6f);
            Depth(rowBody, CardLip, 7f);
            var rank = AddText(rowRt, "Rank", "#1", 44, Navy, TextAlignmentOptions.Center);
            At(rank.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(120f, 70f));
            var avatar = NewRect("Avatar", rowRt);
            At(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(70f, 70f));
            var avatarImg = AddImage(avatar, Circle(), new Color(0.8f, 0.87f, 1f)); avatarImg.raycastTarget = false;
            var flagImg = FlagBadge(rowRt, "Flag", new Vector2(0f, 0.5f), new Vector2(185f, 0f), 64f);
            var nameText = AddText(rowRt, "Name", "Player", 46, Navy, TextAlignmentOptions.Left);
            Stretch(nameText.rectTransform, 232f, 0f, 290f, 0f);
            nameText.enableAutoSizing = true; nameText.fontSizeMin = 26f; nameText.fontSizeMax = 46f;
            var score = AddText(rowRt, "Score", "1000", 46, new Color(0.15f, 0.45f, 0.9f), TextAlignmentOptions.Right);
            At(score.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(190f, 70f));
            var scoreIcon = NewRect("ScoreIcon", rowRt);
            At(scoreIcon, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-240f, 0f), new Vector2(46f, 46f));
            AddImage(scoreIcon, Icon("trophy"), new Color(0.95f, 0.66f, 0.05f)).raycastTarget = false;
            var view = rowRt.gameObject.AddComponent<LeaderboardRowView>();
            var vo = new SerializedObject(view);
            vo.FindProperty("background").objectReferenceValue = rowBody;
            vo.FindProperty("rankText").objectReferenceValue = rank;
            vo.FindProperty("avatar").objectReferenceValue = avatarImg;
            vo.FindProperty("flag").objectReferenceValue = flagImg;
            vo.FindProperty("nameText").objectReferenceValue = nameText;
            vo.FindProperty("scoreText").objectReferenceValue = score;
            vo.ApplyModifiedProperties();

            var empty = AddText(scrollRt, "Empty", "", 44, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            empty.textWrappingMode = TextWrappingModes.Normal;
            At(empty.rectTransform, TopCenter, TopCenter, new Vector2(0f, -200f), new Vector2(860f, 300f));

            var so = new SerializedObject(screen);
            var tabsProp = so.FindProperty("tabButtons"); tabsProp.arraySize = 3;
            var facesProp = so.FindProperty("tabFaces"); facesProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                tabsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];
                facesProp.GetArrayElementAtIndex(i).objectReferenceValue = tabFaces[i];
            }
            so.FindProperty("listRoot").objectReferenceValue = content;
            so.FindProperty("rowTemplate").objectReferenceValue = view;
            so.FindProperty("infoText").objectReferenceValue = info;
            so.FindProperty("emptyText").objectReferenceValue = empty;
            so.FindProperty("myLine").objectReferenceValue = mine;
            so.ApplyModifiedProperties();
            rowRt.gameObject.SetActive(false);
            return s;
        }

        // ==================================================================================================
        //  PROFILE & STATISTICS
        // ==================================================================================================

        /// <summary>One small statistic tile: a big number over a caption.</summary>
        static TMP_Text StatTile(RectTransform parent, string name, float x, float y, Vector2 size, string caption, float valueSize)
        {
            var tile = NewRect(name, parent);
            At(tile, TopCenter, TopCenter, new Vector2(x, y), size);
            Depth(AddImage(tile, Round(), Card, true, 0.5f), CardLip, 8f);
            var v = AddText(tile, "Value", "0", valueSize, new Color(0.15f, 0.45f, 0.9f), TextAlignmentOptions.Center);
            At(v.rectTransform, TopCenter, TopCenter, new Vector2(0f, -8f), new Vector2(size.x - 16f, size.y * 0.58f));
            v.enableAutoSizing = true; v.fontSizeMin = valueSize * 0.45f; v.fontSizeMax = valueSize;
            var l = AddText(tile, "Label", caption, 32, SubText, TextAlignmentOptions.Center);
            At(l.rectTransform, BottomCenter, BottomCenter, new Vector2(0f, 10f), new Vector2(size.x - 12f, size.y * 0.30f));
            l.enableAutoSizing = true; l.fontSizeMin = 18f; l.fontSizeMax = 32f;
            return v;
        }

        /// <summary>
        /// The profile, laid out from the reference: picture, name and country, level with its XP bar, the four headline
        /// numbers across, the rank block, the dice collection strip, then the rest of the statistics.
        ///
        /// The reference's Statistics / Rewards / Achievements tabs are not built: this game has statistics and rewards
        /// but no achievements system, and two tabs where the art shows three would be worse than one honest page.
        /// </summary>
        static RectTransform BuildProfile(RectTransform parent, ScreenRouter router, MenuFlow flow, DiceCollectionPanel dicePanel)
        {
            var s = NewScreen("Screen_Profile", parent);
            Header(s, "My Profile", router);
            var screen = s.gameObject.AddComponent<ProfileScreen>();

            var av = NewRect("AvatarBg", s);
            At(av, TopCenter, TopCenter, new Vector2(0f, -172f), new Vector2(206f, 206f));
            var avBg = AddImage(av, Circle(), new Color(0.22f, 0.58f, 1f));
            Depth(avBg, new Color(0.07f, 0.30f, 0.72f), 9f);
            var pic = NewRect("Picture", av); Stretch(pic, 18f, 18f, 18f, 18f);
            var picImg = AddImage(pic, null, Color.white); picImg.raycastTarget = false; picImg.preserveAspect = true;
            var profileFlag = FlagBadge(av, "Flag", new Vector2(1f, 0f), new Vector2(-18f, 22f), 84f);

            var nameText = AddText(s, "Name", "Player 1", 64, Color.white, TextAlignmentOptions.Center, true);
            At(nameText.rectTransform, TopCenter, TopCenter, new Vector2(0f, -390f), new Vector2(900f, 84f));
            nameText.enableAutoSizing = true; nameText.fontSizeMin = 34f; nameText.fontSizeMax = 64f;
            var tierText = AddText(s, "Tier", "Level 1", 42, Gold, TextAlignmentOptions.Center, true);
            At(tierText.rectTransform, TopCenter, TopCenter, new Vector2(0f, -466f), new Vector2(940f, 60f));
            tierText.enableAutoSizing = true; tierText.fontSizeMin = 26f; tierText.fontSizeMax = 42f;

            var bar = NewRect("XpBar", s);
            At(bar, TopCenter, TopCenter, new Vector2(0f, -530f), new Vector2(660f, 40f));
            AddImage(bar, Round(), new Color(0.05f, 0.16f, 0.45f, 0.85f)).raycastTarget = false;
            var fill = NewRect("Fill", bar); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            AddImage(fill, Round(), new Color(0.3f, 0.85f, 0.4f)).raycastTarget = false;
            var xpText = AddText(bar, "XpText", "0 / 100 XP", 28, Color.white, TextAlignmentOptions.Center, true);
            Stretch(xpText.rectTransform);

            var idText = AddText(s, "FriendId", "", 30, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            At(idText.rectTransform, TopCenter, TopCenter, new Vector2(0f, -578f), new Vector2(940f, 44f));
            idText.enableAutoSizing = true; idText.fontSizeMin = 18f; idText.fontSizeMax = 30f;

            var edit = MakeButton(s, "EditProfileButton", "Edit Profile", "Blue", new Vector2(520f, 104f), Icon("gear"));
            At((RectTransform)edit.transform, TopCenter, TopCenter, new Vector2(0f, -626f), new Vector2(520f, 104f));
            OnClickInt(edit, flow.EditProfile, 0);

            // the four headline numbers, across, as in the reference
            var values = new TMP_Text[10];
            string[] headline = { "Wins", "Losses", "Games", "Win rate" };
            int[] headlineSlot = { 2, 3, 1, 4 };                      // into ProfileScreen's statValues order
            for (int i = 0; i < 4; i++)
                values[headlineSlot[i]] = StatTile(s, "StatTop" + i, -354f + i * 236f, -756f, new Vector2(224f, 158f), headline[i], 60f);

            // the rank block, kept apart from Level / XP above
            var rankCard = NewRect("RankCard", s);
            At(rankCard, TopCenter, TopCenter, new Vector2(0f, -934f), new Vector2(940f, 196f));
            Depth(AddImage(rankCard, Round(), new Color(0.05f, 0.12f, 0.38f, 0.9f), true, 0.5f), new Color(0f, 0.03f, 0.18f, 0.9f), 10f);
            var rankName = AddText(rankCard, "RankName", "BRONZE", 52, Gold, TextAlignmentOptions.Left, true);
            At(rankName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -10f), new Vector2(420f, 64f));
            rankName.enableAutoSizing = true; rankName.fontSizeMin = 30f; rankName.fontSizeMax = 52f;
            var rankPoints = AddText(rankCard, "RankPoints", "Rank Points  0", 36, Color.white, TextAlignmentOptions.Right, true);
            At(rankPoints.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -14f), new Vector2(460f, 56f));
            rankPoints.enableAutoSizing = true; rankPoints.fontSizeMin = 22f; rankPoints.fontSizeMax = 36f;
            var rankBar = NewRect("RankBar", rankCard);
            At(rankBar, TopCenter, TopCenter, new Vector2(0f, -80f), new Vector2(880f, 38f));
            AddImage(rankBar, Round(), new Color(0.02f, 0.06f, 0.24f, 0.9f)).raycastTarget = false;
            var rankFill = NewRect("Fill", rankBar); rankFill.anchorMin = Vector2.zero; rankFill.anchorMax = new Vector2(0f, 1f); rankFill.offsetMin = Vector2.zero; rankFill.offsetMax = Vector2.zero;
            AddImage(rankFill, Round(), new Color(1f, 0.78f, 0.2f)).raycastTarget = false;
            var rankBarText = AddText(rankBar, "BarText", "0 / 500", 26, Color.white, TextAlignmentOptions.Center, true);
            Stretch(rankBarText.rectTransform);
            var rankNext = AddText(rankCard, "RankNext", "Next rank: SILVER", 30, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            At(rankNext.rectTransform, BottomCenter, BottomCenter, new Vector2(0f, 10f), new Vector2(880f, 46f));
            rankNext.enableAutoSizing = true; rankNext.fontSizeMin = 20f; rankNext.fontSizeMax = 30f;

            // the dice collection, as the reference shows it on this screen
            var stripTitle = AddText(s, "DiceTitle", "Dice Collection", 40, Color.white, TextAlignmentOptions.Left, true);
            At(stripTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(76f, -1152f), new Vector2(520f, 58f));
            stripTitle.enableAutoSizing = true; stripTitle.fontSizeMin = 26f; stripTitle.fontSizeMax = 40f;
            var stripAll = MakeButton(s, "OpenDiceButton", "View all", "Blue", new Vector2(230f, 68f), null);
            At((RectTransform)stripAll.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -1146f), new Vector2(230f, 68f));
            OnClick(stripAll, dicePanel.Open);

            var skins = Ludo.Core.DiceSkins.All;
            for (int i = 0; i < 5 && i < skins.Length; i++)
            {
                var cell = NewRect("Dice" + i, s);
                At(cell, TopCenter, TopCenter, new Vector2(-368f + i * 184f, -1222f), new Vector2(164f, 164f));
                var cellBg = AddImage(cell, Round(), Card, true, 0.6f);
                Depth(cellBg, CardLip, 8f);
                var cellButton = cell.gameObject.AddComponent<Button>();
                cellButton.targetGraphic = cellBg; cellButton.transition = Selectable.Transition.None;
                AddButtonFx(cell.gameObject, false);
                OnClick(cellButton, dicePanel.Open);
                var face = NewRect("Face", cell);
                At(face, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(108f, 108f));
                var raw = face.gameObject.AddComponent<RawImage>();
                raw.raycastTarget = false;
                raw.texture = Ludo.Game.DiceSkinLibrary.Icon(skins[i].Id) ?? Ludo.Game.DiceSkinLibrary.Faces(skins[i].Id);
                raw.uvRect = new Rect(0f, 0f, 1f, 1f);           // the rendered 3D dice
            }

            // the rest of the numbers, three across
            string[] rest = { "Coins", "Best streak", "Ranked wins", "Weekly Cup pts", "Disconnects", "Disc. rate" };
            int[] restSlot = { 0, 5, 6, 7, 8, 9 };
            for (int i = 0; i < rest.Length; i++)
                values[restSlot[i]] = StatTile(s, "StatRest" + i, -308f + (i % 3) * 308f, -1406f - (i / 3) * 168f,
                    new Vector2(296f, 156f), rest[i], 48f);

            var so = new SerializedObject(screen);
            so.FindProperty("avatar").objectReferenceValue = picImg;
            so.FindProperty("flag").objectReferenceValue = profileFlag;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("tierText").objectReferenceValue = tierText;
            so.FindProperty("friendIdText").objectReferenceValue = idText;
            so.FindProperty("xpFill").objectReferenceValue = fill;
            so.FindProperty("xpText").objectReferenceValue = xpText;
            var vals = so.FindProperty("statValues"); vals.arraySize = 10;
            for (int i = 0; i < 10; i++) vals.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.FindProperty("rankName").objectReferenceValue = rankName;
            so.FindProperty("rankPointsText").objectReferenceValue = rankPoints;
            so.FindProperty("rankNextText").objectReferenceValue = rankNext;
            so.FindProperty("rankFill").objectReferenceValue = rankFill;
            so.FindProperty("rankBarText").objectReferenceValue = rankBarText;
            so.ApplyModifiedProperties();
            return s;
        }

        /// <summary>One big event card: a coloured banner with an icon badge, a title, a live state line and an action button.</summary>
        static Button EventCard(RectTransform parent, string name, float y, Sprite icon, string title, string action,
            Color face, Color lip, out TMP_Text state, out GameObject dot)
        {
            var rt = NewRect(name, parent);
            At(rt, TopCenter, TopCenter, new Vector2(0f, y), new Vector2(960f, 210f));
            var img = AddImage(rt, Round(), face, true, 0.42f);
            Depth(img, lip, 13f);

            var badge = NewRect("IconBadge", rt);
            At(badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(130f, 130f));
            var badgeImg = AddImage(badge, Round(), new Color(1f, 1f, 1f, 0.20f), true, 0.5f); badgeImg.raycastTarget = false;
            var ic = NewRect("Icon", badge);
            At(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(78f, 78f));
            var icImg = AddImage(ic, icon, Color.white); icImg.raycastTarget = false; icImg.preserveAspect = true;

            var t = AddText(rt, "Title", title, 50, Color.white, TextAlignmentOptions.Left, true);
            At(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(178f, -22f), new Vector2(520f, 66f));
            t.enableAutoSizing = true; t.fontSizeMin = 30f; t.fontSizeMax = 50f;
            t.raycastTarget = false;
            state = AddText(rt, "State", "", 32, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.Left);
            At(state.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(178f, -88f), new Vector2(560f, 52f));
            state.enableAutoSizing = true; state.fontSizeMin = 20f; state.fontSizeMax = 32f;
            state.raycastTarget = false;

            var button = MakeButton(rt, name + "Action", action, "Green", new Vector2(240f, 92f), null);
            At((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 20f), new Vector2(240f, 92f));

            dot = BuildUnreadDot(rt, out var dotText, new Vector2(-12f, -12f), 46f, true);
            dotText.text = "!";
            return button;
        }

        /// <summary>
        /// The Events screen: everything a player can collect outside a match, each with its live state. Only what this
        /// game really has is listed - see EventsScreen for why the reference's Season Pass and timed events are absent.
        /// </summary>
        static RectTransform BuildEvents(RectTransform parent, ScreenRouter router,
            DailyRewardPanel dailyPanel, ChestPanel chestPanel, OnlineMenu menu)
        {
            var s = NewScreen("Screen_Events", parent);
            Header(s, "Events", router);
            NavBar(s, router, null, 0, Friends, Leaderboards, Profile);
            var screen = s.gameObject.AddComponent<EventsScreen>();

            var hint = AddText(s, "Hint", "Free rewards, every day", 36, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, true);
            At(hint.rectTransform, TopCenter, TopCenter, new Vector2(0f, -166f), new Vector2(800f, 50f));
            hint.enableAutoSizing = true; hint.fontSizeMin = 24f; hint.fontSizeMax = 36f;

            var daily = EventCard(s, "CardDaily", -250f, Ikon("calendar"), "Daily Rewards", "Claim",
                new Color(0.55f, 0.28f, 0.86f), new Color(0.30f, 0.12f, 0.54f), out var dailyState, out var dailyDot);
            OnClick(daily, dailyPanel.Open);

            var chest = EventCard(s, "CardChest", -486f, Ikon("chest_color"), "Free Chest", "Open",
                new Color(0.72f, 0.45f, 0.18f), new Color(0.42f, 0.24f, 0.07f), out var chestState, out var chestDot);
            OnClick(chest, chestPanel.Open);

            var cup = EventCard(s, "CardWeeklyCup", -722f, Icon("trophy"), "Weekly Cup", "View",
                new Color(0.93f, 0.55f, 0.12f), new Color(0.60f, 0.30f, 0.03f), out var cupState, out var cupDot);
            OnClickInt(cup, router.Show, Tournaments);
            cupDot.SetActive(false);

            var note = AddText(s, "Note", "More events are on the way. Everything here is free - coins can never be bought.",
                32, new Color(1f, 1f, 1f, 0.62f), TextAlignmentOptions.Center);
            At(note.rectTransform, TopCenter, TopCenter, new Vector2(0f, -980f), new Vector2(880f, 96f));
            note.textWrappingMode = TextWrappingModes.Normal;
            note.enableAutoSizing = true; note.fontSizeMin = 22f; note.fontSizeMax = 32f;

            var so = new SerializedObject(screen);
            so.FindProperty("dailyState").objectReferenceValue = dailyState;
            so.FindProperty("dailyDot").objectReferenceValue = dailyDot;
            so.FindProperty("chestState").objectReferenceValue = chestState;
            so.FindProperty("chestDot").objectReferenceValue = chestDot;
            so.FindProperty("cupState").objectReferenceValue = cupState;
            so.ApplyModifiedProperties();
            return s;
        }

        static TMP_InputField MakeInput(RectTransform parent, string name, string placeholderText, Vector2 position, Vector2 size,
            int characterLimit, float fontSize, TMP_InputField.ContentType contentType)
        {
            var inputRoot = NewRect(name, parent);
            At(inputRoot, TopCenter, TopCenter, position, size);
            AddImage(inputRoot, Round(), Color.white, true, 0.6f);
            var ring = NewRect("Ring", inputRoot); Stretch(ring);
            AddImage(ring, Ring(), new Color(0.55f, 0.68f, 0.95f), true, 0.6f).raycastTarget = false;
            var area = NewRect("Text Area", inputRoot); Stretch(area, 26f, 8f, 26f, 8f);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = AddText(area, "Placeholder", placeholderText, fontSize, new Color(0.5f, 0.55f, 0.65f), TextAlignmentOptions.Center);
            placeholder.fontStyle = FontStyles.Italic; Stretch(placeholder.rectTransform);
            var text = AddText(area, "Text", "", fontSize, Navy, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            var input = inputRoot.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = text; input.placeholder = placeholder;
            input.characterLimit = characterLimit;
            input.contentType = contentType;
            return input;
        }

        /// <summary>A small red circle with a number, in a corner of a button: unread chat messages.</summary>
        static GameObject BuildUnreadDot(RectTransform button, out TMP_Text count, Vector2 offset, float size, bool insideCorner)
        {
            var dot = NewRect("UnreadDot", button);
            var corner = new Vector2(1f, 1f);
            At(dot, corner, corner, insideCorner ? new Vector2(-offset.x - 6f, offset.y - 2f) : offset, new Vector2(size, size));
            AddImage(dot, Circle(), new Color(0.9f, 0.15f, 0.2f)).raycastTarget = false;
            count = AddText(dot, "Count", "1", size * 0.62f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(count.rectTransform);
            dot.gameObject.SetActive(false);
            return dot.gameObject;
        }

        /// <summary>The chat pop-up (message list, text box, Send). The ChatPanel component sits on the button that opens it.</summary>
        static ChatPanel BuildChat(RectTransform parent, Button openButton, GameObject unreadDot, TMP_Text unreadCount)
        {
            var modal = NewRect("ChatModal", parent); Stretch(modal);
            AddImage(modal, null, new Color(0f, 0f, 0.05f, 0.6f)).raycastTarget = true;
            var card = NewRect("Card", modal);
            At(card, TopCenter, TopCenter, new Vector2(0f, -190f), new Vector2(980f, 1320f));   // clear of the turn banner above
            Depth(AddImage(card, Round(), Card, true, 0.45f), CardLip, 14f);

            var title = AddText(card, "Title", "Chat", 64, Navy, TextAlignmentOptions.Center);
            At(title.rectTransform, TopCenter, TopCenter, new Vector2(0f, -22f), new Vector2(500f, 90f));
            var close = MakeRoundButton(card, "CloseButton", "Red", Icon("cross"), 90f);
            At((RectTransform)close.transform, TopCenter, TopCenter, new Vector2(400f, -24f), new Vector2(90f, 90f));

            var view = NewRect("List", card);
            At(view, TopCenter, TopCenter, new Vector2(0f, -125f), new Vector2(920f, 470f));
            AddImage(view, Round(), new Color(0.93f, 0.96f, 1f), true, 0.4f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            var viewport = NewRect("Viewport", view); Stretch(viewport, 14f, 12f, 14f, 12f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var log = AddText(viewport, "Log", "", 38, Navy, TextAlignmentOptions.TopLeft);
            log.textWrappingMode = TextWrappingModes.Normal;
            log.richText = true;
            var logRt = log.rectTransform;
            logRt.anchorMin = new Vector2(0f, 1f); logRt.anchorMax = new Vector2(1f, 1f); logRt.pivot = new Vector2(0.5f, 1f);
            logRt.offsetMin = Vector2.zero; logRt.offsetMax = Vector2.zero;
            var fitter = log.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = logRt;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;

            // one-tap phrases (2 rows of 4): a reaction without typing, sent through the same chat rules
            var quickRow = NewRect("QuickPhrases", card);
            At(quickRow, TopCenter, TopCenter, new Vector2(0f, -608f), new Vector2(920f, 180f));
            var quickButtons = new Button[Ludo.Core.QuickChat.Phrases.Length];
            for (int i = 0; i < quickButtons.Length; i++)
            {
                var cell = NewRect("Quick" + i, quickRow);
                At(cell, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2((i % 4) * 231f + 4f, -(i / 4) * 90f - 4f), new Vector2(223f, 82f));
                var face = AddImage(cell, Round(), new Color(0.90f, 0.94f, 1f), true, 0.6f);
                Depth(face, CardLip, 5f);
                var qb = cell.gameObject.AddComponent<Button>();
                qb.targetGraphic = face; qb.transition = Selectable.Transition.None;
                AddButtonFx(cell.gameObject, false);
                var lbl = AddText(cell, "Label", Ludo.Core.QuickChat.Phrases[i], 34, Navy, TextAlignmentOptions.Center, false);
                Stretch(lbl.rectTransform, 8f, 4f, 8f, 6f);
                lbl.enableAutoSizing = true; lbl.fontSizeMin = 20f; lbl.fontSizeMax = 34f;
                quickButtons[i] = qb;
            }

            // emoji buttons: 3 rows of 10, each sends its emoji at once
            var emojiSprites = EmojiSetup.Ensure();
            var grid = NewRect("Emojis", card);
            At(grid, TopCenter, TopCenter, new Vector2(0f, -800f), new Vector2(920f, 280f));
            var emojiButtons = new Button[Ludo.Core.ChatEmoji.Codes.Length];
            for (int i = 0; i < emojiButtons.Length; i++)
            {
                var cell = NewRect("Emoji" + i, grid);
                At(cell, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2((i % 10) * 92f + 4f, -(i / 10) * 93f - 4f), new Vector2(84f, 84f));
                var face = AddImage(cell, Round(), new Color(0.93f, 0.96f, 1f), true, 0.9f);
                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = face;
                b.transition = Selectable.Transition.None;
                AddButtonFx(cell.gameObject, false);
                var pic = NewRect("Picture", cell); Stretch(pic, 10f, 10f, 10f, 10f);
                var img = AddImage(pic, EmojiSetup.ButtonSprite(Ludo.Core.ChatEmoji.Codes[i]), Color.white);
                img.raycastTarget = false; img.preserveAspect = true;
                emojiButtons[i] = b;
            }

            var input = MakeInput(card, "Input", "Type a message...", new Vector2(-135f, -1098f), new Vector2(640f, 120f),
                Ludo.Core.ChatRules.MaxLength, 40f, TMP_InputField.ContentType.Standard);
            var send = MakeButton(card, "SendButton", "Send", "Green", new Vector2(250f, 120f), null);
            At((RectTransform)send.transform, TopCenter, TopCenter, new Vector2(345f, -1098f), new Vector2(250f, 120f));
            var notice = AddText(card, "Notice", "", 34, new Color(0.8f, 0.2f, 0.1f), TextAlignmentOptions.Center);
            At(notice.rectTransform, TopCenter, TopCenter, new Vector2(0f, -1235f), new Vector2(900f, 60f));

            var panel = openButton.gameObject.AddComponent<ChatPanel>();
            for (int i = 0; i < emojiButtons.Length; i++) OnClickInt(emojiButtons[i], panel.SendEmoji, i);
            for (int i = 0; i < quickButtons.Length; i++) OnClickInt(quickButtons[i], panel.SendQuick, i);
            var so = new SerializedObject(panel);
            so.FindProperty("modal").objectReferenceValue = modal.gameObject;
            so.FindProperty("log").objectReferenceValue = log;
            so.FindProperty("scroll").objectReferenceValue = scroll;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("notice").objectReferenceValue = notice;
            so.FindProperty("sendButton").objectReferenceValue = send;
            so.FindProperty("unreadDot").objectReferenceValue = unreadDot;
            so.FindProperty("unreadText").objectReferenceValue = unreadCount;
            so.FindProperty("emojiSprites").objectReferenceValue = emojiSprites;
            so.ApplyModifiedProperties();
            OnClick(openButton, panel.Toggle);
            OnClick(close, panel.Close);
            OnClick(send, panel.Send);
            PopIn(modal, card);
            modal.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Game screen: the microphone button (online matches only) and the "offline only" buttons.</summary>
        static void BuildGameOnlineExtras(RectTransform safe, GameHud hud, GameObject pausePanel, GameObject resultPanel)
        {
            var mic = MakeRoundButton(safe, "VoiceButton", "Grey", Ico("mic_off"), 124f);
            At((RectTransform)mic.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -36f), new Vector2(124f, 124f));
            var view = mic.gameObject.AddComponent<VoiceHudButton>();
            var vo = new SerializedObject(view);
            vo.FindProperty("button").objectReferenceValue = mic;
            vo.FindProperty("icon").objectReferenceValue = mic.transform.Find("Icon").GetComponent<Image>();
            vo.FindProperty("face").objectReferenceValue = mic.GetComponent<Image>();
            vo.FindProperty("micOn").objectReferenceValue = Ico("mic");
            vo.FindProperty("micOff").objectReferenceValue = Ico("mic_off");
            vo.ApplyModifiedProperties();

            var chat = MakeRoundButton(safe, "ChatButton", "Orange", Ico("chat"), 124f);
            At((RectTransform)chat.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -176f), new Vector2(124f, 124f));
            var dot = BuildUnreadDot((RectTransform)chat.transform, out var dotText, new Vector2(4f, 4f), 46f, false);
            var chatPanel = BuildChat(safe, chat, dot, dotText);
            var cso = new SerializedObject(chatPanel);
            cso.FindProperty("hideWhenOffline").boolValue = true;
            cso.ApplyModifiedProperties();

            var offline = new[]
            {
                pausePanel.transform.Find("Card/RestartButton").gameObject
            };
            var ho = new SerializedObject(hud);
            SetObjects(ho.FindProperty("offlineOnly"), offline);
            var again = resultPanel.transform.Find("Stage/PlayAgainButton");
            ho.FindProperty("playAgainButton").objectReferenceValue = again.gameObject;
            ho.FindProperty("playAgainLabel").objectReferenceValue = again.Find("Label").GetComponent<TMP_Text>();
            ho.ApplyModifiedProperties();
        }
    }
}
