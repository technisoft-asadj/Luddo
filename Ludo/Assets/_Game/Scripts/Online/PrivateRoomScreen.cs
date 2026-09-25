using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Select Mode's "Private Room": a room only the people you give the code to can enter. Three things, all real:
    /// pick the table size and create a room (you get a code to share), type a friend's code to join theirs, or go to
    /// your friends list to invite someone. Private rooms are free to sit in: no coins change hands.
    /// </summary>
    public sealed class PrivateRoomScreen : MonoBehaviour
    {
        [SerializeField] ScreenRouter router;
        [SerializeField] int roomScreen = 9;
        [SerializeField] int welcomeScreen = 14;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text modeText;
        [SerializeField] TMP_InputField codeInput;
        [SerializeField] Image[] sizeChips;                  // 2, 3, 4 players
        [SerializeField] TMP_Text[] sizeLabels;

        static readonly Color ChipOn = new Color(0.22f, 0.78f, 0.34f);
        static readonly Color ChipOff = new Color(0.93f, 0.96f, 1f);
        bool working;
        int size = 2;

        void OnEnable()
        {
            if (!OnlineService.HasChosenLogin)                 // nobody is logged in: never go online by ourselves
            {
                StartCoroutine(ToLoginNextFrame());
                return;
            }
            size = Mathf.Clamp(PlayerPrefs.GetInt("ludo.online.size", 2), RoomService.MinSize, RoomService.MaxSize);
            codeInput.text = "";
            Refresh();
            Connect();
        }

        System.Collections.IEnumerator ToLoginNextFrame()
        {
            yield return null;
            LoginGate.Purpose = LoginPurpose.Online;
            router.Replace(welcomeScreen);
        }

        int TableSize => TableChoice.Seats(ModePicker.Current) > 0 ? TableChoice.Seats(ModePicker.Current) : size;
        ChipRow sizeRow;

        void Refresh()
        {
            bool locked = TableChoice.Seats(ModePicker.Current) > 0;
            if (sizeRow == null) sizeRow = new ChipRow(sizeChips);
            sizeRow.Show(locked ? TableSize - RoomService.MinSize : -1);
            for (int i = 0; i < sizeChips.Length; i++)
            {
                bool on = i + RoomService.MinSize == TableSize;
                sizeChips[i].color = on ? ChipOn : locked ? new Color(0.86f, 0.89f, 0.95f) : ChipOff;
                sizeLabels[i].color = on ? Color.white : new Color(0.10f, 0.16f, 0.35f, locked ? 0.45f : 1f);
            }
            modeText.text = GameSession.ModeName(ModePicker.Current) + "  ·  " + TableSize + " players";
        }

        public void SetSize(int players)
        {
            if (TableChoice.Seats(ModePicker.Current) > 0) { Refresh(); return; }
            size = Mathf.Clamp(players, RoomService.MinSize, RoomService.MaxSize);
            PlayerPrefs.SetInt("ludo.online.size", size);
            PlayerPrefs.Save();
            Refresh();
        }

        async void Connect()
        {
            Say("Connecting...", false);
            bool ok = await OnlineService.ConnectAsync();
            if (this == null) return;
            if (ok)
            {
                Say("Online  -  ready", true);
                _ = SocialService.StartAsync();
            }
            else Say(OnlineService.LastError + "  (tap Create or Join to retry)", false);
        }

        public void Create() => Enter("Creating your room...", () => RoomService.CreatePrivateAsync(TableSize, ModePicker.Current));

        public void Join()
        {
            string code = codeInput.text.Trim();
            if (code.Length == 0) { Say("Type the code your friend gave you.", false); return; }
            Enter("Joining...", () => RoomService.JoinByCodeAsync(code));
        }

        async void Enter(string message, System.Func<Task<bool>> action)
        {
            if (working) return;
            working = true;
            Say(message, true);
            if (!OnlineService.IsReady && !await OnlineService.ConnectAsync())
            {
                working = false;
                if (this != null) Say(OnlineService.LastError, false);
                return;
            }
            bool ok = await action();
            if (this == null) return;
            working = false;
            if (ok) router.Show(roomScreen);
            else Say(RoomService.LastError, false);
        }

        void Say(string text, bool good)
        {
            statusText.text = text;
            statusText.color = good ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.8f, 0.72f);
        }
    }
}
