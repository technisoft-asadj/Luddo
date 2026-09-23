using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// The sound "menu" of the game: which clips belong to which SfxId, how loud and how much pitch variation.
    /// It is a ScriptableObject (a data file in the project), so sounds can be swapped in the Inspector with no code change.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Ludo/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SfxId id;
            public AudioClip[] clips;                       // one is picked at random each time
            [Range(0f, 1f)] public float volume = 1f;
            public Vector2 pitch = new Vector2(1f, 1f);     // random pitch between x and y (a little variation avoids a "machine gun" feel)
        }

        public Entry[] sfx = new Entry[0];
        public AudioClip music;
        [Range(0f, 1f)] public float musicGain = 0.5f;      // background music is quieter than effects at the same slider position

        Dictionary<SfxId, Entry> lookup;

        public bool TryGet(SfxId id, out Entry entry)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<SfxId, Entry>();
                foreach (var e in sfx) if (e != null) lookup[e.id] = e;
            }
            return lookup.TryGetValue(id, out entry);
        }

        void OnValidate() => lookup = null;   // rebuild after edits in the Inspector
    }
}
