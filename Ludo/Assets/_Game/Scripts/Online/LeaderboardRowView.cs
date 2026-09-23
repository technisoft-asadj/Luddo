using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Online
{
    /// <summary>One line of a leaderboard (built once as a template, then copied). Filled in by LeaderboardScreen.</summary>
    public sealed class LeaderboardRowView : MonoBehaviour
    {
        public Image background;          // tinted gold for "me"
        public TMP_Text rankText;
        public Image avatar;
        public Image flag;                // the player's country (hidden if none)
        public TMP_Text nameText;
        public TMP_Text scoreText;
    }
}
