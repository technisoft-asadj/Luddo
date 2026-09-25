using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;
using Ludo.Online;

namespace Ludo.EditorTools
{
    /// <summary>
    /// The screens Select Mode's rows lead to: "how do you want to play?" (for a rule row or a table size), the Private
    /// Room screen and the Tournaments (Weekly Cup) screen. Before these existed several rows only ticked a choice or
    /// dropped the player on Play Online, so tapping them seemed to do nothing.
    /// </summary>
    public static partial class UiSceneBuilder
    {
        static RectTransform BuildPlayHow(RectTransform parent, ScreenRouter router, MenuFlow flow)
        {
            var s = NewScreen("Screen_PlayHow", parent);
            Header(s, "Classic", router);
            var title = s.Find("Title").GetComponent<TMP_Text>();
            title.enableAutoSizing = true; title.fontSizeMin = 48f; title.fontSizeMax = 88f;
            var blurb = AddText(s, "Blurb", "", 36, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Center, true);
            At(blurb.rectTransform, TopCenter, TopCenter, new Vector2(0f, -170f), new Vector2(960f, 100f));
            blurb.textWrappingMode = TextWrappingModes.Normal;
            blurb.enableAutoSizing = true; blurb.fontSizeMin = 22f; blurb.fontSizeMax = 36f;

            var screen = s.gameObject.AddComponent<PlayHowScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("blurb").objectReferenceValue = blurb;
            so.ApplyModifiedProperties();

            var heading = AddText(s, "Heading", "How do you want to play?", 46, Color.white, TextAlignmentOptions.Center, true);
            At(heading.rectTransform, TopCenter, TopCenter, new Vector2(0f, -300f), new Vector2(940f, 64f));
            heading.enableAutoSizing = true; heading.fontSizeMin = 28f; heading.fontSizeMax = 46f;

            const float top = -410f, step = 300f;
            var online = ModeRow(s, "RowOnline", top, Ico("public"), "Play Online", "Real players worldwide, on a coin table",
                "Entry 100+ coins", new Color(1f, 0.93f, 0.74f), new Color(0.95f, 0.70f, 0.13f), out _);
            var friends = ModeRow(s, "RowFriends", top - step, Ico("person_add"), "Play with Friends", "A private room with a code",
                "Free  ·  no coins", new Color(0.90f, 0.85f, 1f), new Color(0.53f, 0.34f, 0.93f), out _);
            var ai = ModeRow(s, "RowAi", top - step * 2f, Robot(), "Play with AI", "Practice against the computer",
                "Easy · Medium · Hard", new Color(0.83f, 0.97f, 0.85f), new Color(0.18f, 0.72f, 0.31f), out _);
            var local = ModeRow(s, "RowPassPlay", top - step * 3f, Icon("multiplayer"), "Pass & Play", "Take turns on this phone",
                "Offline", new Color(0.83f, 0.92f, 1f), new Color(0.20f, 0.56f, 1f), out _);
            OnClick(online, flow.PlayHowOnline);
            OnClick(friends, flow.PlayHowFriends);
            OnClick(ai, flow.PlayHowAi);
            OnClick(local, flow.PlayHowLocal);

            AddSpread(s, new[] { (RectTransform)s.Find("RowOnline"), (RectTransform)s.Find("RowFriends"), (RectTransform)s.Find("RowAi"), (RectTransform)s.Find("RowPassPlay") },
                new[] { 0.15f, 0.50f, 0.90f, 1.30f });
            return s;
        }

        static RectTransform BuildPrivateRoom(RectTransform parent, ScreenRouter router, MenuFlow flow)
        {
            var s = NewScreen("Screen_PrivateRoom", parent);
            Header(s, "Private Room", router);
            var screen = s.gameObject.AddComponent<PrivateRoomScreen>();

            var status = AddText(s, "Status", "Connecting...", 40, Color.white, TextAlignmentOptions.Center, true);
            At(status.rectTransform, TopCenter, TopCenter, new Vector2(0f, -172f), new Vector2(940f, 60f));
            status.enableAutoSizing = true; status.fontSizeMin = 24f; status.fontSizeMax = 40f;

            // ---- create ----
            var createCard = NewRect("CreateCard", s);
            At(createCard, TopCenter, TopCenter, new Vector2(0f, -260f), new Vector2(960f, 470f));
            Depth(AddImage(createCard, Round(), Card, true, 0.45f), CardLip, 14f);
            var cTitle = AddText(createCard, "Title", "Create a room", 54, Navy, TextAlignmentOptions.Left);
            At(cTitle.rectTransform, TopCenter, TopCenter, new Vector2(0f, -24f), new Vector2(880f, 70f));
            var modeText = AddText(createCard, "Mode", "Classic", 36, new Color(0.30f, 0.38f, 0.62f), TextAlignmentOptions.Left);
            At(modeText.rectTransform, TopCenter, TopCenter, new Vector2(0f, -92f), new Vector2(880f, 50f));
            var chips = new Image[3]; var labels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var chip = NewRect("Size" + (i + 2), createCard);
                At(chip, TopCenter, TopCenter, new Vector2((i - 1) * 290f, -156f), new Vector2(272f, 88f));
                chips[i] = AddImage(chip, Round(), new Color(0.93f, 0.96f, 1f), true, 0.5f);
                Depth(chips[i], CardLip, 6f);
                var b = chip.gameObject.AddComponent<Button>();
                b.targetGraphic = chips[i]; b.transition = Selectable.Transition.None;
                AddButtonFx(chip.gameObject, false);
                labels[i] = AddText(chip, "Label", (i + 2) + " Players", 42, Navy, TextAlignmentOptions.Center, false);
                Stretch(labels[i].rectTransform, 0f, 0f, 0f, 4f);
                labels[i].enableAutoSizing = true; labels[i].fontSizeMin = 26f; labels[i].fontSizeMax = 42f;
                OnClickInt(b, screen.SetSize, i + 2);
            }
            var create = MakeButton(createCard, "CreateButton", "Create Room", "Green", new Vector2(700f, 130f), Ico("group"));
            At((RectTransform)create.transform, BottomCenter, BottomCenter, new Vector2(0f, 36f), new Vector2(700f, 130f));
            OnClick(create, screen.Create);

            // ---- join ----
            var joinCard = NewRect("JoinCard", s);
            At(joinCard, TopCenter, TopCenter, new Vector2(0f, -760f), new Vector2(960f, 400f));
            Depth(AddImage(joinCard, Round(), Card, true, 0.45f), CardLip, 14f);
            var jTitle = AddText(joinCard, "Title", "Join with a code", 54, Navy, TextAlignmentOptions.Left);
            At(jTitle.rectTransform, TopCenter, TopCenter, new Vector2(0f, -24f), new Vector2(880f, 70f));
            var jSub = AddText(joinCard, "Sub", "Type the 6-letter code from your friend", 34, new Color(0.30f, 0.38f, 0.62f), TextAlignmentOptions.Left);
            At(jSub.rectTransform, TopCenter, TopCenter, new Vector2(0f, -92f), new Vector2(880f, 46f));
            var input = MakeInput(joinCard, "CodeInput", "CODE", new Vector2(-150f, -172f), new Vector2(480f, 120f), 6, 76, TMP_InputField.ContentType.Alphanumeric);
            var join = MakeButton(joinCard, "JoinButton", "Join", "Blue", new Vector2(300f, 120f), Icon("checkmark"));
            At((RectTransform)join.transform, TopCenter, TopCenter, new Vector2(280f, -172f), new Vector2(300f, 120f));
            OnClick(join, screen.Join);

            // ---- friends ----
            var invite = HeadlineCard(s, "CardInvite", -1200f, Ico("group"), "Invite Friends", "Send an invite from your friends list",
                new Color(0.60f, 0.36f, 0.95f), new Color(0.34f, 0.16f, 0.62f), Color.white, new Color(0.90f, 0.86f, 1f), new Color(0.98f, 0.80f, 0.16f));
            OnClickInt(invite, flow.OpenOnlineScreen, Friends);

            var so = new SerializedObject(screen);
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("roomScreen").intValue = Room;
            so.FindProperty("welcomeScreen").intValue = Welcome;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("modeText").objectReferenceValue = modeText;
            so.FindProperty("codeInput").objectReferenceValue = input;
            SetObjects(so.FindProperty("sizeChips"), chips);
            SetObjects(so.FindProperty("sizeLabels"), labels);
            so.ApplyModifiedProperties();

            AddSpread(s, new[] { createCard, joinCard, (RectTransform)s.Find("CardInvite") }, new[] { 0.05f, 0.30f, 0.60f });
            return s;
        }

        static RectTransform BuildTournaments(RectTransform parent, ScreenRouter router, MenuFlow flow)
        {
            var s = NewScreen("Screen_Tournaments", parent);
            Header(s, "Tournaments", router);
            var screen = s.gameObject.AddComponent<TournamentsScreen>();

            var hint = AddText(s, "Hint", "The Weekly Cup  ·  play ranked, climb the board", 36, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Center, true);
            At(hint.rectTransform, TopCenter, TopCenter, new Vector2(0f, -166f), new Vector2(960f, 50f));
            hint.enableAutoSizing = true; hint.fontSizeMin = 24f; hint.fontSizeMax = 36f;

            // the cup: time left and my points, side by side
            var cup = NewRect("CupCard", s);
            At(cup, TopCenter, TopCenter, new Vector2(0f, -250f), new Vector2(960f, 330f));
            Depth(AddImage(cup, Round(), new Color(1f, 0.93f, 0.76f), true, 0.45f), new Color(0.93f, 0.68f, 0.10f), 14f);
            var badge = NewRect("Badge", cup);
            At(badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(150f, 150f));
            var badgeImg = AddImage(badge, Circle(), new Color(0.95f, 0.70f, 0.13f)); badgeImg.raycastTarget = false;
            Depth(badgeImg, new Color(0.60f, 0.38f, 0.03f), 8f, 0f);
            var cupIcon = NewRect("Icon", badge);
            At(cupIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(92f, 92f));
            AddImage(cupIcon, Ikon("k_award"), Color.white).raycastTarget = false;

            var dark = new Color(0.30f, 0.18f, 0.02f);
            var t1 = AddText(cup, "TitleLeft", "ENDS IN", 32, new Color(0.55f, 0.36f, 0.06f), TextAlignmentOptions.Center);
            At(t1.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -40f), new Vector2(340f, 44f));
            var timeLeft = AddText(cup, "TimeLeft", "0d 0h", 70, dark, TextAlignmentOptions.Center);
            At(timeLeft.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -96f), new Vector2(340f, 110f));
            timeLeft.enableAutoSizing = true; timeLeft.fontSizeMin = 36f; timeLeft.fontSizeMax = 70f;
            var t2 = AddText(cup, "TitleRight", "YOUR POINTS", 32, new Color(0.55f, 0.36f, 0.06f), TextAlignmentOptions.Center);
            At(t2.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(340f, 44f));
            var points = AddText(cup, "Points", "0", 70, dark, TextAlignmentOptions.Center);
            At(points.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -96f), new Vector2(340f, 110f));
            points.enableAutoSizing = true; points.fontSizeMin = 36f; points.fontSizeMax = 70f;
            var name = AddText(cup, "CupName", "WEEKLY CUP", 44, dark, TextAlignmentOptions.Center);
            At(name.rectTransform, BottomCenter, BottomCenter, new Vector2(70f, 26f), new Vector2(700f, 64f));

            // how it works
            var how = NewRect("HowCard", s);
            At(how, TopCenter, TopCenter, new Vector2(0f, -620f), new Vector2(960f, 330f));
            Depth(AddImage(how, Round(), Card, true, 0.45f), CardLip, 14f);
            var hTitle = AddText(how, "Title", "How it works", 50, Navy, TextAlignmentOptions.Left);
            At(hTitle.rectTransform, TopCenter, TopCenter, new Vector2(0f, -24f), new Vector2(880f, 66f));
            var hBody = AddText(how, "Body", "", 40, new Color(0.22f, 0.30f, 0.55f), TextAlignmentOptions.TopLeft);
            At(hBody.rectTransform, TopCenter, TopCenter, new Vector2(0f, -100f), new Vector2(880f, 210f));
            hBody.textWrappingMode = TextWrappingModes.Normal;
            hBody.enableAutoSizing = true; hBody.fontSizeMin = 26f; hBody.fontSizeMax = 40f;

            var play = MakeButton(s, "PlayRanked", "Play Ranked Match", "Green", new Vector2(900f, 150f), Ico("flash_on"));
            At((RectTransform)play.transform, TopCenter, TopCenter, new Vector2(0f, -1010f), new Vector2(900f, 150f));
            OnClickInt(play, flow.PlayOnlineAtSize, 2);
            var board = MakeButton(s, "OpenBoard", "Weekly Cup Board", "Blue", new Vector2(900f, 150f), Icon("trophy"));
            At((RectTransform)board.transform, TopCenter, TopCenter, new Vector2(0f, -1190f), new Vector2(900f, 150f));
            OnClickInt(board, flow.OpenWeeklyCup, Leaderboards);

            var so = new SerializedObject(screen);
            so.FindProperty("timeLeft").objectReferenceValue = timeLeft;
            so.FindProperty("myPoints").objectReferenceValue = points;
            so.FindProperty("howItWorks").objectReferenceValue = hBody;
            so.ApplyModifiedProperties();

            AddSpread(s, new[] { cup, how, (RectTransform)play.transform, (RectTransform)board.transform }, new[] { 0.05f, 0.25f, 0.55f, 0.75f });
            return s;
        }
    }
}
