using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;
using Ludo.Online;

namespace Ludo.EditorTools
{
    /// <summary>The worldwide Quick Match screen: globe, search time, players found, dots, Cancel - and the "Match found" list.</summary>
    public static partial class UiSceneBuilder
    {
        static RectTransform BuildQuickMatch(RectTransform parent, ScreenRouter router)
        {
            var s = NewScreen("Screen_QuickMatch", parent);
            Header(s, "Quick Match", router);
            var screen = s.gameObject.AddComponent<QuickMatchScreen>();
            var title = s.Find("Title").GetComponent<TMP_Text>();

            // ================= searching =================
            var search = NewRect("SearchPanel", s); Stretch(search);

            // what is being searched for: the rules, the table size and the entry (read-only chips, filled by the screen)
            var chipTexts = new TMP_Text[3];
            float[] chipW = { 300f, 280f, 300f };
            float chipX = -(chipW[0] + chipW[1] + chipW[2] + 40f) / 2f;
            for (int i = 0; i < 3; i++)
            {
                var chip = NewRect("Chip" + i, search);
                At(chip, Mid, Mid, new Vector2(chipX + chipW[i] / 2f, 800f), new Vector2(chipW[i], 84f));
                chipX += chipW[i] + 20f;
                var body = AddImage(chip, Round(), i == 0 ? new Color(1f, 0.82f, 0.15f) : new Color(0.05f, 0.12f, 0.36f, 0.9f), true, 0.5f);
                body.raycastTarget = false;
                Depth(body, i == 0 ? new Color(0.72f, 0.5f, 0.02f) : new Color(0.02f, 0.06f, 0.22f), 6f);
                chipTexts[i] = AddText(chip, "Label", "", 38, i == 0 ? Navy : Color.white, TextAlignmentOptions.Center, false);
                Stretch(chipTexts[i].rectTransform, 6f, 0f, 6f, 4f);
                chipTexts[i].enableAutoSizing = true; chipTexts[i].fontSizeMin = 22f; chipTexts[i].fontSizeMax = 38f;
                chipTexts[i].raycastTarget = false;
            }

            var globe = NewRect("Globe", search);
            At(globe, Mid, Mid, new Vector2(0f, 400f), new Vector2(400f, 400f));
            var globeShadow = NewRect("Glow", globe);
            At(globeShadow, Mid, Mid, Vector2.zero, new Vector2(620f, 620f));
            AddImage(globeShadow, Load(Generated + "glow_radial.png"), new Color(0.3f, 0.6f, 1f, 0.55f)).raycastTarget = false;
            var globeIcon = NewRect("Icon", globe);
            At(globeIcon, Mid, Mid, Vector2.zero, new Vector2(400f, 400f));
            AddImage(globeIcon, Ikon("globe_color"), Color.white).raycastTarget = false;
            globe.gameObject.AddComponent<Bob>();

            var status = AddText(search, "Status", "Searching for players...", 58, Color.white, TextAlignmentOptions.Center, true);
            status.textWrappingMode = TextWrappingModes.Normal;
            status.enableAutoSizing = true; status.fontSizeMin = 30f; status.fontSizeMax = 58f;
            At(status.rectTransform, Mid, Mid, new Vector2(0f, 90f), new Vector2(940f, 90f));
            var wait = AddText(search, "Wait", "Please wait while we find real players for you.", 34, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            wait.textWrappingMode = TextWrappingModes.Normal;
            At(wait.rectTransform, Mid, Mid, new Vector2(0f, 10f), new Vector2(940f, 50f));
            wait.enableAutoSizing = true; wait.fontSizeMin = 22f; wait.fontSizeMax = 34f;

            var label = AddText(search, "PlayersFound", "Players found", 44, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            At(label.rectTransform, Mid, Mid, new Vector2(0f, -80f), new Vector2(700f, 60f));
            var found = AddText(search, "Found", "0 / 2", 96, Color.white, TextAlignmentOptions.Center, true);
            At(found.rectTransform, Mid, Mid, new Vector2(0f, -180f), new Vector2(700f, 120f));

            // progress towards a full table
            var track = NewRect("BarTrack", search);
            At(track, Mid, Mid, new Vector2(0f, -290f), new Vector2(760f, 44f));
            AddImage(track, Round(), new Color(0.02f, 0.06f, 0.22f, 0.85f), true, 3f).raycastTarget = false;
            var fillRt = NewRect("Fill", track); Stretch(fillRt, 4f, 4f, 4f, 4f);
            var bar = AddImage(fillRt, Round(), new Color(0.32f, 0.86f, 0.40f), true, 3.4f);
            bar.type = Image.Type.Filled; bar.fillMethod = Image.FillMethod.Horizontal; bar.fillOrigin = 0; bar.fillAmount = 0f;
            bar.raycastTarget = false;

            var timer = AddText(search, "Timer", "00:00", 110, Gold, TextAlignmentOptions.Center, true);
            At(timer.rectTransform, Mid, Mid, new Vector2(0f, -390f), new Vector2(700f, 140f));

            var dots = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var dot = NewRect("Dot" + i, search);
                At(dot, Mid, Mid, new Vector2((i - 1.5f) * 70f, -500f), new Vector2(44f, 44f));
                dots[i] = AddImage(dot, Circle(), new Color(1f, 1f, 1f, 0.3f));
                dots[i].raycastTarget = false;
            }

            // ================= match found =================
            var foundPanel = NewRect("FoundPanel", s); Stretch(foundPanel);
            var foundTitle = AddText(foundPanel, "FoundTitle", "MATCH FOUND!", 104, Gold, TextAlignmentOptions.Center, true);
            At(foundTitle.rectTransform, Mid, Mid, new Vector2(0f, 700f), new Vector2(980f, 140f));
            var rows = new FoundRow[4];
            for (int i = 0; i < 4; i++)
            {
                var row = NewRect("Row" + i, foundPanel);
                At(row, Mid, Mid, new Vector2(0f, 480f - i * 175f), new Vector2(940f, 150f));
                var body = AddImage(row, Round(), DarkPanel, true, 0.5f);
                Depth(body, DarkPanelLip, 10f);
                var av = NewRect("Avatar", row);
                At(av, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(112f, 112f));
                AddImage(av, Circle(), new Color(0.80f, 0.87f, 1f)).raycastTarget = false;
                var pic = NewRect("Picture", av); Stretch(pic, 8f, 8f, 8f, 8f);
                var picImg = AddImage(pic, null, Color.white); picImg.raycastTarget = false; picImg.preserveAspect = true;
                var flagImg = FlagBadge(av, "Flag", new Vector2(1f, 0f), new Vector2(-22f, 10f), 50f);
                var name = AddText(row, "Name", "Player", 56, Color.white, TextAlignmentOptions.Left, true);
                At(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 24f), new Vector2(740f, 70f));
                name.enableAutoSizing = true; name.fontSizeMin = 30f; name.fontSizeMax = 56f;
                var info = AddText(row, "Info", "Level 1  -  Bronze 1000", 40, Gold, TextAlignmentOptions.Left, true);
                At(info.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -36f), new Vector2(740f, 56f));
                rows[i] = new FoundRow { root = row.gameObject, avatar = picImg, flag = flagImg, nameText = name, infoText = info };
            }
            var countdown = AddText(foundPanel, "Countdown", "Starting in 3", 68, Color.white, TextAlignmentOptions.Center, true);
            At(countdown.rectTransform, Mid, Mid, new Vector2(0f, -290f), new Vector2(900f, 90f));

            // ================= buttons (both panels) =================
            var retry = MakeButton(s, "RetryButton", "Try Again", "Green", new Vector2(620f, 140f), Icon("checkmark"));
            At((RectTransform)retry.transform, Mid, Mid, new Vector2(0f, -640f), new Vector2(620f, 140f));
            var cancel = MakeButton(s, "CancelButton", "Cancel", "Red", new Vector2(620f, 140f), null);
            At((RectTransform)cancel.transform, BottomCenter, BottomCenter, new Vector2(0f, 200f), new Vector2(620f, 140f));
            OnClick(retry, screen.Retry);
            OnClick(cancel, screen.Cancel);

            var so = new SerializedObject(screen);
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("searchPanel").objectReferenceValue = search.gameObject;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.FindProperty("foundText").objectReferenceValue = found;
            SetObjects(so.FindProperty("dots"), dots);
            so.FindProperty("progressBar").objectReferenceValue = bar;
            SetObjects(so.FindProperty("chips"), chipTexts);
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.FindProperty("foundPanel").objectReferenceValue = foundPanel.gameObject;
            so.FindProperty("foundCountdown").objectReferenceValue = countdown;
            var rowsProp = so.FindProperty("foundRows");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                var p = rowsProp.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                p.FindPropertyRelative("avatar").objectReferenceValue = rows[i].avatar;
                p.FindPropertyRelative("flag").objectReferenceValue = rows[i].flag;
                p.FindPropertyRelative("nameText").objectReferenceValue = rows[i].nameText;
                p.FindPropertyRelative("infoText").objectReferenceValue = rows[i].infoText;
            }
            so.ApplyModifiedProperties();
            foundPanel.gameObject.SetActive(false);
            return s;
        }
    }
}
