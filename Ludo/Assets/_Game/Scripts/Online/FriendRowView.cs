using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludo.Online
{
    /// <summary>
    /// One line in the friends list (built once as a template, then copied). Filled in by FriendsScreen.
    /// A MonoBehaviour must live in a file with the same name as its class, or Unity cannot find it in a build.
    /// </summary>
    public sealed class FriendRowView : MonoBehaviour
    {
        public Image dot;                 // green = online
        public TMP_Text nameText;
        public Button buttonA;
        public TMP_Text labelA;
        public Image colorA;
        public Button buttonB;
        public TMP_Text labelB;
        public Image colorB;
    }
}
