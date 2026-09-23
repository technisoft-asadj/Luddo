using System.Collections.Generic;
using UnityEngine;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// The country flags (one atlas picture + where each flag sits in it), loaded from Resources. Any script asks for a flag by
    /// ISO country code; an unknown or empty code gives null, and the caller simply hides the flag. Sprites are made on first use.
    /// Built by Ludo > Build Country Flags from Art/Flags (public-domain flags, see ThirdParty/RegionFlags).
    /// </summary>
    public sealed class FlagLibrary : ScriptableObject
    {
        public Texture2D atlas;
        public string[] codes = new string[0];
        public RectInt[] rects = new RectInt[0];

        static FlagLibrary instance;
        static readonly Dictionary<string, Sprite> made = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            made.Clear();
        }

        static FlagLibrary Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<FlagLibrary>("FlagLibrary");
                return instance;
            }
        }

        /// <summary>The flag of this country, or null (no country chosen, unknown code, or the library is missing).</summary>
        public static Sprite Get(string countryCode)
        {
            string code = Countries.Normalize(countryCode);
            if (code.Length == 0) return null;
            if (made.TryGetValue(code, out var sprite) && sprite != null) return sprite;
            var lib = Instance;
            if (lib == null || lib.atlas == null) return null;
            int i = System.Array.IndexOf(lib.codes, code);
            if (i < 0 || i >= lib.rects.Length) return null;
            var r = lib.rects[i];
            sprite = Sprite.Create(lib.atlas, new Rect(r.x, r.y, r.width, r.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "flag_" + code;
            made[code] = sprite;
            return sprite;
        }

        /// <summary>Show a country's flag on an image, or hide the image when there is none.</summary>
        public static void Apply(UnityEngine.UI.Image image, string countryCode)
        {
            if (image == null) return;
            var flag = Get(countryCode);
            image.sprite = flag;
            image.preserveAspect = true;
            image.gameObject.SetActive(flag != null);
        }
    }
}
