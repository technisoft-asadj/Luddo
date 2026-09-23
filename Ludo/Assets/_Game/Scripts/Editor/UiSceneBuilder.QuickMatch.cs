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

            var globe = NewRect("Globe", search);
            At(globe, Mid, Mid, new Vector2(0f, 470f), new Vector2(320f, 320f));
            var globeBg = AddImage(globe, Circle(), new Color(0.22f, 0.58f, 1f));
            Depth(globeBg, new Color(0.07f, 0.30f, 0.72f), 14f);
            var globeShine = NewRect("Shine", globe);
            At(globeShine, Mid, Mid, new Vector2(0f, 70f), new Vector2(220f, 130f));
            AddImage(globeShine, Circle(), new Color(1f, 1f, 1f, 0.22f)).raycastTarget = false;
            var globeIcon = NewRect("Icon", globe);
            At(globeIcon, Mid, Mid, new Vector2(0f, 6f), new Vector2(210f, 210f));
            AddImage(globeIcon, Ico("public"), Color.white).raycastTarget = false;
            globeIcon.gameObject.AddComponent<Spin>();

            var world = AddText(search, "Worldwide", "Worldwide", 68, Color.white, TextAlignmentOptions.Center, true);
            At(world.rectTransform, Mid, Mid, new Vector2(0f, 250f), new Vector2(900f, 90f));

            var status = AddText(search, "Status", "Searching for players...", 46, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.Center);
            status.textWrappingMode = TextWrappingModes.Normal;
            status.enableAutoSizing = true; status.fontSizeMin = 28f; status.fontSizeMax = 46f;
            At(status.rectTransform, Mid, Mid, new Vector2(0f, 150f), new Vector2(940f, 80f));

            var timer = AddText(search, "Timer", "00:00", 140, Gold, TextAlignmentOptions.Center, true);
            At(timer.rectTransform, Mid, Mid, new Vector2(0f, -10f), new Vector2(700f, 170f));

            var label = AddText(search, "PlayersFound", "Players Found", 46, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            At(label.rectTransform, Mid, Mid, new Vector2(0f, -180f), new Vector2(700f, 70f));
            var found = AddText(search, "Found", "0 / 2", 112, Color.white, TextAlignmentOptions.Center, true);
            At(found.rectTransform, Mid, Mid, new Vector2(0f, -290f), new Vector2(700f, 140f));

            var dots = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var dot = NewRect("Dot" + i, search);
                At(dot, Mid, Mid, new Vector2((i - 1.5f) * 92f, -415f), new Vector2(60f, 60f));
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
            At((RectTransform)retry.transform, Mid, Mid, new Vector2(0f, -560f), new Vector2(620f, 140f));
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
