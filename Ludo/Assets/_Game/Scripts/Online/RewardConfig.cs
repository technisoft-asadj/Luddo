using UnityEngine;
using Ludo.Core;

namespace Ludo.Online
{
    /// <summary>
    /// Every reward, rank and disconnect number as an asset (Assets/_Game/Resources/RewardConfig.asset): Rank Points per win and
    /// loss, XP, coins, the rank thresholds, the ad multiplier and the reconnect grace period. Change them in the Inspector, no
    /// code needed. If the asset is missing the defaults in RewardSettings are used.
    /// </summary>
    [CreateAssetMenu(fileName = "RewardConfig", menuName = "Ludo/Reward Config")]
    public sealed class RewardConfig : ScriptableObject
    {
        public RewardSettings settings = new RewardSettings();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            var asset = Resources.Load<RewardConfig>("RewardConfig");
            RewardSettings.Active = asset != null && asset.settings != null && asset.settings.tiers != null && asset.settings.tiers.Length > 0
                ? asset.settings : new RewardSettings();
        }
    }
}
