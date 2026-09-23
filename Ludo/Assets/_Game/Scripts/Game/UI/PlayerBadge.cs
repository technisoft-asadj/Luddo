using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Game
{
    /// <summary>A coloured name tag next to one corner of the board, with the player's picture. On that player's turn it glows, bounces and brightens.</summary>
    public sealed class PlayerBadge : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image avatarImage;     // the player's chosen picture in the round avatar
        [SerializeField] Image flagImage;       // online: the country the player chose, small on the picture's corner (hidden if none)
        [SerializeField] Image glow;            // bright ring, only visible on this player's turn
        [SerializeField] GameObject speaking;   // green ring + microphone, shown while this player talks in voice chat
        [SerializeField] GameObject timerRoot;  // online: the bar that empties while this player thinks (takes the place of the subtitle)
        [SerializeField] Image timerFill;       // stretched over the bar; its right edge (anchor) moves left as the time runs out
        [SerializeField] TMP_Text timerLabel;   // seconds left
        [SerializeField] Color calm = new Color(0.30f, 0.88f, 0.40f);
        [SerializeField] Color warning = new Color(1f, 0.70f, 0.15f);
        [SerializeField] Color urgent = new Color(0.96f, 0.30f, 0.32f);

        bool isTurn;
        bool isSpeaking;

        public string PlayerName => nameText.text;
        public Sprite Avatar => avatarImage != null ? avatarImage.sprite : null;

        public void Setup(string playerName, string subtitle, Color color, Sprite avatar, Sprite flag = null)
        {
            gameObject.SetActive(true);
            background.color = color;
            // dark text on light colours (yellow), white text on dark ones
            Color text = color.grayscale > 0.6f ? new Color(0.1f, 0.1f, 0.2f) : Color.white;
            nameText.text = playerName;
            nameText.color = text;
            subtitleText.text = subtitle;
            subtitleText.color = text;
            subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            HideTimer();
            if (avatarImage != null)
            {
                avatarImage.sprite = avatar;
                avatarImage.enabled = avatar != null;
            }
            if (flagImage != null)
            {
                flagImage.sprite = flag;
                flagImage.gameObject.SetActive(flag != null);
            }
            SetTurn(false);
        }

        /// <summary>Show how much of this player's turn time is left. Called every frame while they think, so the bar drains smoothly.</summary>
        public void ShowTimer(float remaining, float total)
        {
            if (timerRoot == null) return;
            if (!timerRoot.activeSelf)
            {
                timerRoot.SetActive(true);
                subtitleText.gameObject.SetActive(false);           // the bar takes the subtitle's place
            }
            float k = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            var fr = timerFill.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(k, 1f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            timerFill.color = remaining > 8f ? calm : remaining > 4f ? Color.Lerp(warning, calm, (remaining - 4f) / 4f) : Color.Lerp(urgent, warning, remaining / 4f);
            timerLabel.text = Mathf.CeilToInt(remaining).ToString();
        }

        public void HideTimer()
        {
            if (timerRoot == null || !timerRoot.activeSelf) return;
            timerRoot.SetActive(false);
            subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitleText.text));
        }

        public void Hide()
        {
            HideTimer();
            SetSpeaking(false);
            gameObject.SetActive(false);
        }

        /// <summary>Show that this player is talking (online voice chat).</summary>
        public void SetSpeaking(bool on)
        {
            if (isSpeaking == on) return;
            isSpeaking = on;
            if (speaking != null) speaking.SetActive(on);
        }

        public void SetTurn(bool on)
        {
            isTurn = on;
            group.alpha = on ? 1f : 0.74f;
            if (glow != null) glow.gameObject.SetActive(on);
            if (!on) transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (!isTurn) return;
            float wave = Mathf.Sin(Time.unscaledTime * 6f);
            transform.localScale = Vector3.one * (1.04f + 0.03f * wave);
            if (glow != null) glow.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * wave);
        }
    }
}
