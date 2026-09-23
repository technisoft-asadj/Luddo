using TMPro;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>Replaces "{version}" in this label with the installed game's version (the scene is built before the release version is set).</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class VersionText : MonoBehaviour
    {
        void Awake()
        {
            var label = GetComponent<TMP_Text>();
            label.text = label.text.Replace("{version}", Application.version);
        }
    }
}
