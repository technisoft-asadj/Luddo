using System;

namespace Ludo.Core
{
    /// <summary>The tunable numbers of the worldwide Quick Match search (seconds). Defaults are the documented rule in PLAN.md.</summary>
    [Serializable]
    public sealed class MatchmakingSettings
    {
        /// <summary>How long a Quick room waits for a FULL table before it may start smaller.</summary>
        public float fullTableWait = 20f;
        /// <summary>The search gives up after this long ("Not enough players found").</summary>
        public float searchTimeout = 60f;
        /// <summary>A lone host looks for a better room to join this often.</summary>
        public float mergePoll = 4f;
        /// <summary>How long the "Match found" list stays on screen before the game starts.</summary>
        public float matchFoundSeconds = 2.5f;
        /// <summary>The fewest people a Quick match may start with.</summary>
        public int minPlayers = 2;
    }

    /// <summary>
    /// The rules of the Quick Match / Ranked search, kept free of any network code so they can be tested:
    ///   * a full table always starts; a Ranked table must be exactly full (Elo needs the exact table);
    ///   * a Quick table may start smaller once the wait for a full table is over and at least minPlayers are there;
    ///   * the search stops when the timeout is reached (never filled up with computer players);
    ///   * a host who is alone moves to a room that already has more people (tie: the smaller room id wins), so people
    ///     searching at the same time end up together instead of each waiting alone in their own room.
    /// </summary>
    public static class MatchmakingRules
    {
        public static bool ShouldStart(int players, int tableSize, bool ranked, float searchSeconds, MatchmakingSettings s)
        {
            if (players >= tableSize) return true;
            if (ranked) return false;
            return players >= s.minPlayers && searchSeconds >= s.fullTableWait;
        }

        public static bool TimedOut(float searchSeconds, MatchmakingSettings s) => searchSeconds >= s.searchTimeout;

        /// <summary>Should a host with myPlayers people (only a host alone ever moves) join another open room?</summary>
        public static bool ShouldMerge(int myPlayers, string myRoomId, int otherPlayers, string otherRoomId)
        {
            if (myPlayers != 1) return false;
            if (string.IsNullOrEmpty(otherRoomId) || otherRoomId == myRoomId) return false;
            if (otherPlayers > myPlayers) return true;
            return otherPlayers == myPlayers && string.CompareOrdinal(otherRoomId, myRoomId) < 0;
        }

        /// <summary>The line under the timer.</summary>
        public static string Phase(float searchSeconds, MatchmakingSettings s) =>
            searchSeconds < s.fullTableWait ? "Searching for players..." : searchSeconds < s.searchTimeout ? "Still searching - widening the search..." : "Not enough players found";
    }
}
