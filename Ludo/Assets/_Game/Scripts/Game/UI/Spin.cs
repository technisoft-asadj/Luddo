using UnityEngine;

namespace Ludo.Game
{
    /// <summary>Turns an element at a constant speed (the sunburst behind the winner). Unscaled time: it also turns while paused.</summary>
    public sealed class Spin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 12f;

        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
