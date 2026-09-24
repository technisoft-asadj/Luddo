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
            out OnlineMenu menu, out GameObject joinModal, out GameObject busyOverlay)
        {
            var s = NewScreen("Screen_Online", parent);
            Header(s, "Play Online", router);
            menu = s.gameObject.AddComponent<OnlineMenu>();

            // status line under the title (tap it to retry when it says the connection failed)
            var statusRt = NewRect("StatusButton", s);
            At(statusRt, TopCenter, TopCenter, new Vector2(0f, -185f), new Vector2(940f, 80f));
            var hit = AddImage(statusRt, null, new Color(0f, 0f, 0f, 0f)); hit.raycastTarget = true;
            var statusButton = statusRt.gameObject.AddComponent<Button>();
            statusButton.targetGraphic = hit; statusButton.transition = Selectable.Transition.None;
            var status = AddText(statusRt, "Status", "Connecting...", 44, Color.white, TextAlignmentOptions.Center, true);
            Stretch(status.rectTransform);
            status.enableAutoSizing = true; status.fontSizeMin = 26f; status.fontSizeMax = 44f;
            OnClick(statusButton, menu.Retry);

            // profile card: picture, name, tier and rating; tap to open the profile and statistics
            var card0 = NewRect("ProfileCard", s);
            At(card0, TopCenter, TopCenter, new Vector2(0f, -265f), new Vector2(940f, 150f));
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
            var cardName = AddText(card0, "Name", "Player 1", 56, Color.white, TextAlignmentOptions.Left, true);
            At(cardName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 24f), new Vector2(600f, 70f));
            cardName.enableAutoSizing = true; cardName.fontSizeMin = 30f; cardName.fontSizeMax = 56f;
            var cardRating = AddText(card0, "Rating", "Bronze  1000", 42, Gold, TextAlignmentOptions.Left, true);
            At(cardRating.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -36f), new Vector2(600f, 56f));
            var cardArrow = NewRect("Chevron", card0);
            At(cardArrow, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, 0f), new Vector2(54f, 54f));
            cardArrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
            AddImage(cardArrow, Load(KenneyUi + "Icons/arrow_basic_w.png"), new Color(0.55f, 0.65f, 0.85f)).raycastTarget = false;
            OnClick(cardButton, menu.OpenProfile);

            // how many players at the table: 2, 3 or 4 (used by Ranked, Quick Match and Create Room)
            var chips = new Image[3]; var chipLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var chip = NewRect("Size" + (i + 2), s);
                At(chip, TopCenter, TopCenter, new Vector2((i - 1) * 315f, -440f), new Vector2(295f, 100f));
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
            BuildModePicker(s, -555f, withRule: false);

            // the coin table for Quick Match (entry fee; the winner takes the pot). Private rooms are always free.
            var fees = Ludo.Core.CoinTables.Fees;
            var feeChips = new Image[fees.Length]; var feeLabels = new TMP_Text[fees.Length];
            var feeTitle = AddText(s, "EntryLabel", "Entry", 38, Color.white, TextAlignmentOptions.Left, true);
            At(feeTitle.rectTransform, TopCenter, TopCenter, new Vector2(-400f, -675f), new Vector2(160f, 88f));
            for (int i = 0; i < fees.Length; i++)
            {
                var chip = NewRect("Fee" + fees[i], s);
                At(chip, TopCenter, TopCenter, new Vector2(-235f + i * 160f, -675f), new Vector2(148f, 88f));
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

            // daily reward: a gift button in the corner (red dot when today's reward is waiting)
            var gift = MakeRoundButton(s, "DailyButton", "Orange", Icon2("star"), 116f);
            At((RectTransform)gift.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -36f), new Vector2(116f, 116f));
            OnClick(gift, menu.OpenDaily);
            var giftDot = BuildUnreadDot((RectTransform)gift.transform, out var giftDotText, new Vector2(-4f, -4f), 40f, true);
            giftDotText.text = "!";

            // the two ways to play with real players around the world
            var quick = CardButton(s, "CardQuickMatch", -790f, Ico("flash_on"), "Quick Match", "Ranked - play right now with anyone", new Color(0.22f, 0.78f, 0.34f), true);
            OnClick(quick, menu.QuickMatch);

            // everything else as a grid of tiles
            var create = TileButton(s, "TileCreateRoom", -250f, -1010f, Ico("group"), "Create Room", new Color(0.22f, 0.58f, 1f));
            var join = TileButton(s, "TileJoinRoom", 250f, -1010f, Ico("person_add"), "Join Code", new Color(1f, 0.66f, 0.14f));
            var friends = TileButton(s, "TileFriends", -250f, -1190f, Icon("multiplayer"), "Friends", new Color(0.66f, 0.40f, 0.96f));
            var boards = TileButton(s, "TileLeaderboards", 250f, -1190f, Icon("trophy"), "Leaderboards", new Color(0.95f, 0.72f, 0.10f));
            var cup = TileButton(s, "TileWeeklyCup", -250f, -1370f, Icon2("star"), "Weekly Cup", new Color(0.96f, 0.30f, 0.32f));
            var accountButton = TileButton(s, "TileAccount", 250f, -1370f, Icon("gear"), "Account", new Color(0.45f, 0.55f, 0.75f));
            OnClick(create, menu.CreateRoom);
            OnClick(join, menu.OpenJoin);
            OnClickInt(friends, router.Show, Friends);
            OnClick(boards, menu.OpenLeaderboards);
            OnClick(cup, menu.OpenTournament);
            OnClick(accountButton, menu.OpenAccount);

            var note = AddText(s, "SafetyNote", "Be kind! You can mute, block or report any player.", 38, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            At(note.rectTransform, BottomCenter, BottomCenter, new Vector2(0f, 150f), new Vector2(960f, 60f));
            note.enableAutoSizing = true; note.fontSizeMin = 24f; note.fontSizeMax = 38f;

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
            so.FindProperty("profileAvatar").objectReferenceValue = cardPicImg;
            so.FindProperty("profileFlag").objectReferenceValue = cardFlag;
            SetObjects(so.FindProperty("feeChips"), feeChips);
            SetObjects(so.FindProperty("feeLabels"), feeLabels);
            so.FindProperty("giftDot").objectReferenceValue = giftDot;
            BuildDailyReward(root, out var dailyPanel);
            so.FindProperty("daily").objectReferenceValue = dailyPanel;
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

            // ----- the four seats -----
            int[] seatOrder = Ludo.Core.Board.DefaultSeats(4);
            for (int i = 0; i < 4; i++)
            {
                var rowRt = NewRect("Seat" + (i + 1), s);
                At(rowRt, TopCenter, TopCenter, new Vector2(0f, -640f - i * 160f), new Vector2(940f, 140f));
                var body = AddImage(rowRt, Round(), new Color(0.10f, 0.19f, 0.47f, 0.92f), true, 0.5f);
                Depth(body, DarkPanelLip, 8f);

                var glow = NewRect("Speaking", rowRt); Stretch(glow, -8f, -8f, -8f, -8f);
                AddImage(glow, Ring(), SpeakGreen, true, 0.5f).raycastTarget = false;

                var colorBar = NewRect("SeatColor", rowRt);
                At(colorBar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(22f, 96f));
                var barImg = AddImage(colorBar, Round(), SeatStyle.Colors[seatOrder[i]], true, 0.4f); barImg.raycastTarget = false;

                var av = NewRect("Avatar", rowRt);
                At(av, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(112f, 112f));
                AddImage(av, Circle(), new Color(0.80f, 0.87f, 1f)).raycastTarget = false;
                var pic = NewRect("Picture", av); Stretch(pic, 8f, 8f, 8f, 8f);
                var picImg = AddImage(pic, null, Color.white); picImg.raycastTarget = false; picImg.preserveAspect = true;
                var flagImg = FlagBadge(av, "Flag", new Vector2(1f, 0f), new Vector2(-22f, 10f), 50f);

                var nameText = AddText(rowRt, "Name", "Waiting...", 54, Color.white, TextAlignmentOptions.Left, true);
                At(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 14f), new Vector2(560f, 70f));
                nameText.enableAutoSizing = true; nameText.fontSizeMin = 28f; nameText.fontSizeMax = 54f;
                var tag = AddText(rowRt, "Tag", "", 32, Gold, TextAlignmentOptions.Left, true);
                At(tag.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, -34f), new Vector2(560f, 44f));

                var more = MakeRoundButton(rowRt, "MoreButton", "Grey", Ico("flag"), 92f);
                At((RectTransform)more.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(92f, 92f));
                OnClickInt(more, room.OpenMore, i);

                seatRefs[i] = new SeatRefs { root = rowRt.gameObject, avatar = picImg, flag = flagImg, seatColor = barImg, nameText = nameText, tagText = tag, glow = glow.gameObject, more = more };
            }

            var notice = AddText(s, "Notice", "", 40, Gold, TextAlignmentOptions.Center, true);
            At(notice.rectTransform, TopCenter, TopCenter, new Vector2(0f, -1290f), new Vector2(940f, 60f));
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
            At(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 900f));
            Depth(AddImage(card, Round(), Card, true, 0.45f), CardLip, 14f);
            title = AddText(card, "Title", "Player", 66, Navy, TextAlignmentOptions.Center);
            At(title.rectTransform, TopCenter, TopCenter, new Vector2(0f, -35f), new Vector2(760f, 95f));
            title.enableAutoSizing = true; title.fontSizeMin = 34f; title.fontSizeMax = 66f;

            var mute = MakeButton(card, "MuteButton", "Mute / Unmute", "Blue", new Vector2(700f, 130f), Ico("mic_off"));
            At((RectTransform)mute.transform, TopCenter, TopCenter, new Vector2(0f, -170f), new Vector2(700f, 130f));
            var block = MakeButton(card, "BlockButton", "Block", "Orange", new Vector2(700f, 130f), Ico("block"));
            At((RectTransform)block.transform, TopCenter, TopCenter, new Vector2(0f, -330f), new Vector2(700f, 130f));
            var report = MakeButton(card, "ReportButton", "Report", "Red", new Vector2(700f, 130f), Ico("flag"));
            At((RectTransform)report.transform, TopCenter, TopCenter, new Vector2(0f, -490f), new Vector2(700f, 130f));
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

            var message = AddText(s, "Message", "", 40, Gold, TextAlignmentOptions.Center, true);
            At(message.rectTransform, TopCenter, TopCenter, new Vector2(0f, -545f), new Vector2(940f, 60f));
            message.enableAutoSizing = true; message.fontSizeMin = 24f; message.fontSizeMax = 40f;

            // scrolling list
            var scrollRt = NewRect("List", s);
            At(scrollRt, TopCenter, TopCenter, new Vector2(0f, -625f), new Vector2(940f, 1190f));
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
            Stretch(nameText.rectTransform, 232f, 0f, 250f, 0f);
            nameText.enableAutoSizing = true; nameText.fontSizeMin = 26f; nameText.fontSizeMax = 46f;
            var score = AddText(rowRt, "Score", "1000", 46, new Color(0.15f, 0.45f, 0.9f), TextAlignmentOptions.Right);
            At(score.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(220f, 70f));
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

        static RectTransform BuildProfile(RectTransform parent, ScreenRouter router, MenuFlow flow)
        {
            var s = NewScreen("Screen_Profile", parent);
            Header(s, "My Profile", router);
            var screen = s.gameObject.AddComponent<ProfileScreen>();

            var av = NewRect("AvatarBg", s);
            At(av, TopCenter, TopCenter, new Vector2(0f, -190f), new Vector2(260f, 260f));
            var avBg = AddImage(av, Circle(), new Color(0.22f, 0.58f, 1f));
            Depth(avBg, new Color(0.07f, 0.30f, 0.72f), 9f);
            var pic = NewRect("Picture", av); Stretch(pic, 22f, 22f, 22f, 22f);
            var picImg = AddImage(pic, null, Color.white); picImg.raycastTarget = false; picImg.preserveAspect = true;
            var profileFlag = FlagBadge(av, "Flag", new Vector2(1f, 0f), new Vector2(-24f, 30f), 104f);

            var nameText = AddText(s, "Name", "Player 1", 72, Color.white, TextAlignmentOptions.Center, true);
            At(nameText.rectTransform, TopCenter, TopCenter, new Vector2(0f, -450f), new Vector2(900f, 90f));
            nameText.enableAutoSizing = true; nameText.fontSizeMin = 36f; nameText.fontSizeMax = 72f;
            var tierText = AddText(s, "Tier", "Level 1", 50, Gold, TextAlignmentOptions.Center, true);
            At(tierText.rectTransform, TopCenter, TopCenter, new Vector2(0f, -535f), new Vector2(940f, 70f));
            tierText.enableAutoSizing = true; tierText.fontSizeMin = 30f; tierText.fontSizeMax = 50f;

            // XP bar: dark rounded track, coloured fill (its right anchor moves), the numbers on top
            var bar = NewRect("XpBar", s);
            At(bar, TopCenter, TopCenter, new Vector2(0f, -612f), new Vector2(720f, 44f));
            AddImage(bar, Round(), new Color(0.05f, 0.16f, 0.45f, 0.85f)).raycastTarget = false;
            var fill = NewRect("Fill", bar); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            AddImage(fill, Round(), new Color(0.3f, 0.85f, 0.4f)).raycastTarget = false;
            var xpText = AddText(bar, "XpText", "0 / 100 XP", 30, Color.white, TextAlignmentOptions.Center, true);
            Stretch(xpText.rectTransform);

            var idText = AddText(s, "FriendId", "", 32, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            At(idText.rectTransform, TopCenter, TopCenter, new Vector2(0f, -665f), new Vector2(940f, 46f));
            idText.enableAutoSizing = true; idText.fontSizeMin = 20f; idText.fontSizeMax = 32f;

            var edit = MakeButton(s, "EditProfileButton", "Edit Profile", "Blue", new Vector2(640f, 120f), Icon("gear"));
            At((RectTransform)edit.transform, TopCenter, TopCenter, new Vector2(0f, -715f), new Vector2(640f, 120f));
            OnClickInt(edit, flow.EditProfile, 0);

            // the rank block: rank name, Rank Points, how far to the next rank (kept apart from Level / XP above)
            var rankCard = NewRect("RankCard", s);
            At(rankCard, TopCenter, TopCenter, new Vector2(0f, -850f), new Vector2(940f, 285f));
            Depth(AddImage(rankCard, Round(), new Color(0.05f, 0.12f, 0.38f, 0.9f), true, 0.5f), new Color(0f, 0.03f, 0.18f, 0.9f), 10f);
            var rankName = AddText(rankCard, "RankName", "BRONZE", 66, Gold, TextAlignmentOptions.Center, true);
            At(rankName.rectTransform, TopCenter, TopCenter, new Vector2(0f, -14f), new Vector2(880f, 80f));
            rankName.enableAutoSizing = true; rankName.fontSizeMin = 36f; rankName.fontSizeMax = 66f;
            var rankPoints = AddText(rankCard, "RankPoints", "Rank Points  0", 44, Color.white, TextAlignmentOptions.Center, true);
            At(rankPoints.rectTransform, TopCenter, TopCenter, new Vector2(0f, -92f), new Vector2(880f, 56f));
            var rankBar = NewRect("RankBar", rankCard);
            At(rankBar, TopCenter, TopCenter, new Vector2(0f, -160f), new Vector2(800f, 42f));
            AddImage(rankBar, Round(), new Color(0.02f, 0.06f, 0.24f, 0.9f)).raycastTarget = false;
            var rankFill = NewRect("Fill", rankBar); rankFill.anchorMin = Vector2.zero; rankFill.anchorMax = new Vector2(0f, 1f); rankFill.offsetMin = Vector2.zero; rankFill.offsetMax = Vector2.zero;
            AddImage(rankFill, Round(), new Color(1f, 0.78f, 0.2f)).raycastTarget = false;
            var rankBarText = AddText(rankBar, "BarText", "0 / 500", 28, Color.white, TextAlignmentOptions.Center, true);
            Stretch(rankBarText.rectTransform);
            var rankNext = AddText(rankCard, "RankNext", "Next rank: SILVER", 34, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            At(rankNext.rectTransform, TopCenter, TopCenter, new Vector2(0f, -218f), new Vector2(880f, 50f));
            rankNext.enableAutoSizing = true; rankNext.fontSizeMin = 22f; rankNext.fontSizeMax = 34f;

            // ten statistic tiles (2 columns x 5 rows)
            string[] labels = { "Coins", "Games", "Wins", "Losses", "Win rate", "Best streak", "Ranked wins", "Weekly Cup pts", "Disconnects", "Disconnect rate" };
            var values = new TMP_Text[10];
            for (int i = 0; i < 10; i++)
            {
                var tile = NewRect("Stat" + i, s);
                At(tile, TopCenter, TopCenter, new Vector2(-235f + (i % 2) * 470f, -1165f - (i / 2) * 190f), new Vector2(450f, 172f));
                Depth(AddImage(tile, Round(), Card, true, 0.5f), CardLip, 9f);
                var v = AddText(tile, "Value", "0", 72, new Color(0.15f, 0.45f, 0.9f), TextAlignmentOptions.Center);
                At(v.rectTransform, TopCenter, TopCenter, new Vector2(0f, -12f), new Vector2(420f, 96f));
                var l = AddText(tile, "Label", labels[i], 36, SubText, TextAlignmentOptions.Center);
                At(l.rectTransform, BottomCenter, BottomCenter, new Vector2(0f, 12f), new Vector2(420f, 50f));
                l.enableAutoSizing = true; l.fontSizeMin = 22f; l.fontSizeMax = 36f;
                values[i] = v;
            }

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

        /// <summary>A rounded text box in the style of the profile pop-up. Anchored top-centre.</summary>
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
