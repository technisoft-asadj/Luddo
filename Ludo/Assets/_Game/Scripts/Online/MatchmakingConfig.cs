using UnityEngine;
using Ludo.Core;

namespace Ludo.Online
{
    /// <summary>
    /// The Quick Match search numbers as an asset (Assets/_Game/Resources/MatchmakingConfig.asset) so they can be tuned without
    /// code. If the asset is missing the built-in defaults from MatchmakingSettings are used.
    /// </summary>
    [CreateAssetMenu(fileName = "MatchmakingConfig", menuName = "Ludo/Matchmaking Config")]
    public sealed class MatchmakingConfig : ScriptableObject
    {
        public MatchmakingSettings settings = new MatchmakingSettings();

        static MatchmakingSettings cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cached = null;

        public static MatchmakingSettings Current
        {
            get
            {
                if (cached != null) return cached;
                var asset = Resources.Load<MatchmakingConfig>("MatchmakingConfig");
                cached = asset != null && asset.settings != null ? asset.settings : new MatchmakingSettings();
                return cached;
            }
        }
    }
}
