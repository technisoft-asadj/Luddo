using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Friends: your own ID to give to friends, a box to add somebody by their ID, incoming requests, your friends
    /// (green dot = online) and, when you are in a room, an Invite button next to each online friend.
    /// </summary>
    public sealed class FriendsScreen : MonoBehaviour
    {
        [SerializeField] TMP_Text myIdText;
        [SerializeField] TMP_InputField addInput;
        [SerializeField] TMP_Text messageText;
        [SerializeField] RectTransform listRoot;
        [SerializeField] FriendRowView rowTemplate;
        [SerializeField] TMP_Text headerTemplate;
        [SerializeField] TMP_Text emptyText;
        [SerializeField] Color green = new Color(0.22f, 0.80f, 0.32f);
        [SerializeField] Color red = new Color(0.96f, 0.30f, 0.32f);
        [SerializeField] Color blue = new Color(0.20f, 0.56f, 1f);

        readonly List<GameObject> spawned = new List<GameObject>();
        bool busy;

        void OnEnable()
        {
            messageText.text = "";
            rowTemplate.gameObject.SetActive(false);
            headerTemplate.gameObject.SetActive(false);
            SocialService.Changed += Refresh;
            Begin();
        }

        void OnDisable() => SocialService.Changed -= Refresh;

        async void Begin()
        {
            myIdText.text = "Connecting...";
            bool ok = await SocialService.StartAsync();
            if (this == null) return;
            if (!ok) { myIdText.text = "Offline"; Say(SocialService.LastError); return; }
            Refresh();
        }

        void Refresh()
        {
            if (this == null || !isActiveAndEnabled) return;
            myIdText.text = OnlineService.IsReady ? SocialService.MyFriendId.Replace('_', ' ') : "Offline";
            foreach (var o in spawned) if (o != null) Destroy(o);
            spawned.Clear();

            var incoming = SocialService.IncomingRequests();
            var friends = SocialService.Friends();
            var sent = SocialService.OutgoingRequests();
            bool inRoom = RoomService.InRoom;

            if (incoming.Count > 0)
            {
                Header("Friend requests");
                foreach (var f in incoming)
                {
                    var id = f.Id;
                    Row(f, "Accept", green, () => Run(SocialService.AcceptAsync(id)), "No", red, () => Run(SocialService.DeclineAsync(id)));
                }
            }

            Header("Friends (" + friends.Count + ")");
            foreach (var f in friends)
            {
                var id = f.Id;
                bool canInvite = inRoom && f.Online;
                Row(f,
                    canInvite ? "Invite" : null, blue, () => Invite(id),
                    "Remove", red, () => Run(SocialService.RemoveAsync(id)));
            }

            if (sent.Count > 0)
            {
                Header("Sent requests");
                foreach (var f in sent)
                {
                    var id = f.Id;
                    Row(f, null, blue, null, "Cancel", red, () => Run(SocialService.CancelRequestAsync(id)));
                }
            }

            emptyText.gameObject.SetActive(friends.Count == 0 && incoming.Count == 0 && sent.Count == 0);
        }

        void Header(string text)
        {
            var h = Instantiate(headerTemplate, listRoot);
            h.gameObject.SetActive(true);
            h.text = text;
            spawned.Add(h.gameObject);
        }

        void Row(FriendInfo f, string labelA, Color colorA, UnityEngine.Events.UnityAction onA,
                 string labelB, Color colorB, UnityEngine.Events.UnityAction onB)
        {
            var row = Instantiate(rowTemplate, listRoot);
            row.gameObject.SetActive(true);
            row.nameText.text = f.Name;
            row.dot.color = f.Online ? new Color(0.3f, 0.95f, 0.4f) : new Color(0.55f, 0.6f, 0.72f);
            Setup(row.buttonA, row.labelA, row.colorA, labelA, colorA, onA);
            Setup(row.buttonB, row.labelB, row.colorB, labelB, colorB, onB);
            spawned.Add(row.gameObject);
        }

        static void Setup(Button b, TMP_Text label, Image face, string text, Color color, UnityEngine.Events.UnityAction action)
        {
            b.gameObject.SetActive(text != null);
            if (text == null) return;
            label.text = text;
            face.color = color;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(action);
        }

        // ---------- actions ----------

        public void CopyMyId()
        {
            GUIUtility.systemCopyBuffer = SocialService.MyFriendId;
            Say("ID copied");
        }

        public void AddFriend() => AddFriendAsync();

        async void AddFriendAsync()
        {
            if (busy) return;
            busy = true;
            bool ok = await SocialService.AddByNameAsync(addInput.text);
            busy = false;
            if (this == null) return;
            if (ok) { addInput.text = ""; Say("Friend request sent"); }
            else Say(SocialService.LastError);
        }

        async void Run(System.Threading.Tasks.Task<bool> task)
        {
            bool ok = await task;
            if (this == null) return;
            if (!ok) Say(SocialService.LastError);
        }

        async void Invite(string memberId)
        {
            var mine = RoomService.Code;
            bool ok = await SocialService.InviteAsync(memberId, mine, GameSettings.PlayerName(0), GameSettings.AvatarIndex(0));
            if (this == null) return;
            Say(ok ? "Invite sent" : SocialService.LastError);
        }

        void Say(string message)
        {
            messageText.text = message;
            CancelInvoke(nameof(ClearMessage));
            Invoke(nameof(ClearMessage), 3.5f);
        }

        void ClearMessage() { if (messageText != null) messageText.text = ""; }
    }
}
