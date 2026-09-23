using UnityEngine;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>The house rules as an asset you can edit in the Inspector (Assets/_Game/Data/DefaultRules.asset).
    /// The Core engine has no Unity code, so this thin wrapper is how Unity holds its RulesConfig.</summary>
    [CreateAssetMenu(menuName = "Ludo/Rules Config", fileName = "DefaultRules")]
    public sealed class RulesConfigAsset : ScriptableObject
    {
        [SerializeField] RulesConfig rules = new RulesConfig();
        public RulesConfig Rules => rules;
    }
}
