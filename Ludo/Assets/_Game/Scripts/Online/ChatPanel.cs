using System.Collections;
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

        // a short-lived preview bubble, boxed and clipped so a long message never spills past its edges: shown near the
        // Chat button for a new message while the chat window is closed, then fades away by itself.
        [SerializeField] RectTransform previewBubble;
        [SerializeField] CanvasGroup previewGroup;
        [SerializeField] TMP_Text previewText;
        const float PreviewHold = 3.5f;
        Coroutine previewRoutine;

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
            HidePreview();
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
            if (!IsOpen && !m.mine)
            {
                unread++;
                ShowUnread();
                ShowPreview(m);
            }
        }

        /// <summary>A boxed, self-clipped bubble with the sender and message, faded in near the Chat button for a few
        /// seconds. Long text is truncated with an ellipsis so it can never spill outside the box.</summary>
        void ShowPreview(ChatMessage m)
        {
            if (previewBubble == null || previewText == null) return;
            string name = ChatEmoji.ToRichText(m.name.Replace("<", "").Replace(">", ""));
            string text = ChatEmoji.ToRichText(m.text.Replace("<", "").Replace(">", ""));
            previewText.text = "<b>" + name + "</b>  " + text;
            if (previewRoutine != null) StopCoroutine(previewRoutine);
            previewRoutine = StartCoroutine(PlayPreview());
        }

        IEnumerator PlayPreview()
        {
            previewBubble.gameObject.SetActive(true);
            const float inTime = 0.18f, outTime = 0.25f;
            for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime)
            {
                previewGroup.alpha = Mathf.Clamp01(t / inTime);
                yield return null;
            }
            previewGroup.alpha = 1f;
            for (float t = 0f; t < PreviewHold; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < outTime; t += Time.unscaledDeltaTime)
            {
                previewGroup.alpha = 1f - Mathf.Clamp01(t / outTime);
                yield return null;
            }
            previewGroup.alpha = 0f;
            previewBubble.gameObject.SetActive(false);
            previewRoutine = null;
        }

        void HidePreview()
        {
            if (previewRoutine != null) { StopCoroutine(previewRoutine); previewRoutine = null; }
            if (previewBubble != null) previewBubble.gameObject.SetActive(false);
            if (previewGroup != null) previewGroup.alpha = 0f;
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
            HidePreview();
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
