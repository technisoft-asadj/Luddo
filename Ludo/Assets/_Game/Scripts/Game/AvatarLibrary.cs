using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// The pictures a player can choose from (round animal faces) and the picture computer players use (a robot).
    /// Loaded from Resources so any script can ask for an avatar by number. Choosing from a fixed set means the app
    /// never touches the phone's photos or camera, so it needs no extra permission and collects no personal image data.
    /// </summary>
    [CreateAssetMenu(fileName = "AvatarLibrary", menuName = "Ludo/Avatar Library")]
    public sealed class AvatarLibrary : ScriptableObject
    {
        public Sprite[] avatars = new Sprite[0];
        public Sprite computer;

        static AvatarLibrary instance;

        static AvatarLibrary Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<AvatarLibrary>("AvatarLibrary");
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        /// <summary>How many avatars a player can pick from.</summary>
        public static int Count => Instance != null ? Instance.avatars.Length : 0;

        /// <summary>The avatar with this number (wraps round, so any number is safe). Null only if the library is missing.</summary>
        public static Sprite Get(int index)
        {
            var lib = Instance;
            if (lib == null || lib.avatars.Length == 0) return null;
            return lib.avatars[((index % lib.avatars.Length) + lib.avatars.Length) % lib.avatars.Length];
        }

        /// <summary>The picture shown for computer players.</summary>
        public static Sprite Computer => Instance != null ? Instance.computer : null;
    }
}
