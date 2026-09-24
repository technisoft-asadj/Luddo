using UnityEngine;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// The face pictures of the collectable dice designs (Ludo.Core.DiceSkins), loaded from Resources so the dice, the
    /// collection screen and the menus can all ask for one by id. Each entry is only a texture: the model, the physics
    /// throw and the number the engine rolled are the same whichever design is equipped, so a design can never be luckier.
    /// Rebuild with the menu Ludo > Setup Dice Skins.
    /// </summary>
    [CreateAssetMenu(fileName = "DiceSkinLibrary", menuName = "Ludo/Dice Skin Library")]
    public sealed class DiceSkinLibrary : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string id;
            public Texture2D faces;
        }

        public Entry[] skins = new Entry[0];

        static DiceSkinLibrary instance;

        static DiceSkinLibrary Instance => instance != null ? instance : instance = Resources.Load<DiceSkinLibrary>("DiceSkinLibrary");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        /// <summary>The face atlas of a design, or the default one's (null only if the library is missing).</summary>
        public static Texture2D Faces(string id)
        {
            var lib = Instance;
            if (lib == null) return null;
            Texture2D fallback = null;
            foreach (var s in lib.skins)
            {
                if (s.id == id) return s.faces;
                if (s.id == DiceSkins.Default) fallback = s.faces;
            }
            return fallback;
        }

        /// <summary>The atlas of the design this phone has chosen.</summary>
        public static Texture2D Equipped() => Faces(GameSettings.DiceSkin);
    }
}
