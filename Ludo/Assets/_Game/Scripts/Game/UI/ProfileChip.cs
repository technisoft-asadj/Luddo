using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Game
{
    /// <summary>The small "picture + name" tag on the main menu. It always shows the saved profile of one player and, when tapped, opens the profile editor.</summary>
    public sealed class ProfileChip : MonoBehaviour
    {
        [SerializeField] int player;
        [SerializeField] Image avatar;
        [SerializeField] TMP_Text nameText;

        void OnEnable()
        {
            GameSettings.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => GameSettings.Changed -= Refresh;

        void Refresh()
        {
            avatar.sprite = GameSettings.Picture(player);
            nameText.text = GameSettings.PlayerName(player);
        }
    }
}
