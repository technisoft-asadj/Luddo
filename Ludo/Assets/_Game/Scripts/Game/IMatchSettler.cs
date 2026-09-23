using System.Threading.Tasks;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// What the game needs from the online ranking code when a match ends. The game says HOW the match ended; the settler
    /// decides what it is worth (RewardRules), records it and returns the numbers for the result screen.
    /// </summary>
    public interface IMatchSettler
    {
        /// <summary>The match has a winner (a person or a computer). 'byForfeit': every other person stopped playing.</summary>
        Task<MatchSummary> Finished(int winner, bool winnerIsPerson, bool byForfeit);

        /// <summary>This phone's player left or dropped out before the end. Null when nothing is charged (a friendly game).</summary>
        MatchSummary Left(ResultKind kind);

        /// <summary>Nobody can tell who is at fault: nothing changes for anybody.</summary>
        void Void();

        /// <summary>The ads SDK confirmed a completed rewarded ad: add the Rank Point bonus (once per match). Returns the updated summary.</summary>
        MatchSummary ClaimAdBonus();
    }
}
