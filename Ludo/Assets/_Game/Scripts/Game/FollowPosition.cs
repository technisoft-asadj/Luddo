using UnityEngine;

namespace Ludo.Game
{
    /// <summary>Keeps this object on top of another object's position (the dice tray under the dice).
    /// It runs after GameController, which moves the dice in its own LateUpdate.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FollowPosition : MonoBehaviour
    {
        [SerializeField] Transform target;

        void LateUpdate()
        {
            if (target != null) transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
        }
    }
}
