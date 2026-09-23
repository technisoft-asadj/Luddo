using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Game
{
    /// <summary>
    /// The time limit of an online turn: a bar under the "Player 1's turn" banner that empties, with the seconds left.
    /// It turns orange and then red when time is nearly up. GameController tells it what to show.
    /// </summary>
    public sealed class TurnTimerBar : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Image fill;
        [SerializeField] TMP_Text label;
        [SerializeField] Color calm = new Color(0.30f, 0.88f, 0.40f);
        [SerializeField] Color warning = new Color(1f, 0.70f, 0.15f);
        [SerializeField] Color urgent = new Color(0.96f, 0.30f, 0.32f);

        public void Show(float remaining, float total)
        {
            if (!root.activeSelf) root.SetActive(true);
            float k = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            fill.fillAmount = k;
            fill.color = remaining > 8f ? calm : remaining > 4f ? warning : urgent;
            label.text = Mathf.CeilToInt(remaining).ToString();
            label.color = remaining > 4f ? Color.white : urgent;
        }

        public void Hide()
        {
            if (root.activeSelf) root.SetActive(false);
        }
    }
}
