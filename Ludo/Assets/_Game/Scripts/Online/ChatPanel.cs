using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;

namespace Ludo.Online
{
    /// <summary>
    /// Text chat window for the waiting room and for the match. It sits on the always-visible "Chat" button and opens a
    /// pop-up with the message list, a text box and a Send button. Messages travel through MatchLink (which the room and
    /// the game share), so what was said in the room is still there during the match. Unread messages light a small dot.
    /// </summary>
    public sealed class ChatPanel : MonoBehaviour
    {
        [SerializeField] GameObject modal;
        [SerializeField] TMP_Text log;
        [SerializeField] ScrollRect scroll;
        [SerializeField] TMP_InputField input;
        [SerializeField] TMP_Text notice;
        [SerializeField] Button sendButton;
        [SerializeField] GameObject unreadDot;
        [SerializeField] TMP_Text unreadText;
        [SerializeField] TMP_SpriteAsset emojiSprites;
        [SerializeField] bool hideWhenOffline;          // the game screen's chat button: there is nobody to chat with in an offline game

        MatchLink link;
        readonly StringBuilder lines = new StringBuilder();
        int unread;

        public bool IsOpen => modal != null && modal.activeSelf;

        void Start()
        {
            if (hideWhenOffline && !Ludo.Game.GameSession.IsOnline) gameObject.SetActive(false);
        }

        void Awake()
        {
            if (emojiSprites == null) return;
            if (log != null) log.spriteAsset = emojiSprites;
            if (input != null && input.textComponent != null) input.textComponent.spriteAsset = emojiSprites;
        }

        void OnEnable()
        {
            if (modal != null) modal.SetActive(false);
            unread = 0;
            ShowUnread();
            if (input != null) input.onSubmit.AddListener(OnSubmit);
        }

        void OnDisable()
        {
            if (input != null) input.onSubmit.RemoveListener(OnSubmit);
            Unbind();
        }

        void Update()
        {
            if (MatchLink.Current != link) Bind(MatchLink.Current);
        }

        void Bind(MatchLink next)
        {
            Unbind();
            link = next;
            lines.Length = 0;
            if (link == null) { if (log != null) log.text = ""; return; }
            link.ChatReceived += OnMessage;
            foreach (var m in link.ChatHistory)
                if (!SafetyService.IsBlocked(m.playerId) || m.mine) lines.AppendLine(Format(m));
            Render();
        }

        void Unbind()
        {
            if (link != null) link.ChatReceived -= OnMessage;
            link = null;
        }

        void OnMessage(ChatMessage m)
        {
            lines.AppendLine(Format(m));
            Render();
            if (!IsOpen && !m.mine) { unread++; ShowUnread(); }
        }

        /// <summary>One line, coloured name first. Tag characters are removed so nobody can inject formatting.</summary>
        static string Format(ChatMessage m)
        {
            string name = m.mine ? "You" : ChatEmoji.ToRichText(m.name.Replace("<", "").Replace(">", ""));
            string text = ChatEmoji.ToRichText(m.text.Replace("<", "").Replace(">", ""));
            string colour = m.mine ? "#1B8F3A" : "#C2410C";
            return "<b><color=" + colour + ">" + name + "</color></b>  " + text;
        }

        void Render()
        {
            if (log == null) return;
            log.text = lines.ToString();
            if (IsOpen) ScrollToEnd();
        }

        void ScrollToEnd()
        {
            if (scroll == null) return;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }

        void ShowUnread()
        {
            if (unreadDot != null) unreadDot.SetActive(unread > 0);
            if (unreadText != null) unreadText.text = unread > 9 ? "9+" : unread.ToString();
        }

        // ---------- buttons ----------

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            if (modal == null) return;
            modal.SetActive(true);
            unread = 0;
            ShowUnread();
            notice.text = MatchLink.Current == null ? "Chat works once you are in a room." : "";
            ScrollToEnd();
        }

        public void Close()
        {
            if (modal != null) modal.SetActive(false);
            if (input != null) input.DeactivateInputField();
        }

        void OnSubmit(string _) => Send();

        public void Send()
        {
            var current = MatchLink.Current;
            if (current == null) { notice.text = "Chat works once you are in a room."; return; }
            if (current.SendChat(input.text, out string problem))
            {
                input.text = "";
                notice.text = "";
                input.ActivateInputField();
            }
            else notice.text = problem;
        }

        /// <summary>
        /// A quick-reaction button ("Nice move!", "Good game!"): sends that phrase at once, through the same chat path as a
        /// typed message, so the filter and the anti-spam throttle still apply.
        /// </summary>
        public void SendQuick(int index)
        {
            if (!QuickChat.IsValidIndex(index)) return;
            var current = MatchLink.Current;
            if (current == null) { notice.text = "Chat works once you are in a room."; return; }
            notice.text = current.SendChat(QuickChat.Get(index), out string problem) ? "" : problem;
        }

        /// <summary>An emoji button: sends that emoji straight away as its own message.</summary>
        public void SendEmoji(int index)
        {
            if (index < 0 || index >= ChatEmoji.Codes.Length) return;
            var current = MatchLink.Current;
            if (current == null) { notice.text = "Chat works once you are in a room."; return; }
            notice.text = current.SendChat(ChatEmoji.Text(ChatEmoji.Codes[index]), out string problem) ? "" : problem;
        }
    }
}
