namespace Ludo.Game
{
    /// <summary>
    /// What GameController needs from an online match, and nothing more (the online code lives in another assembly).
    /// Every phone runs the same rules engine. The HOST alone decides dice values; players only say "roll" and "I pick
    /// move number N". The host checks that the sender really owns the seat, then tells everybody the value / the move.
    /// Messages are queued, so nothing is lost while a scene is still loading. All Take... methods return false when
    /// nothing is waiting.
    /// </summary>
    public interface IMatchLink
    {
        /// <summary>True on the phone that hosts the room (it rolls the dice and referees).</summary>
        bool IsHost { get; }

        /// <summary>False once the connection to the host is gone (the game cannot continue).</summary>
        bool IsConnected { get; }

        // ----- the host tells everybody -----
        void BroadcastRoll(int value);
        void BroadcastPick(int moveIndex);
        void BroadcastTakeover(int player);

        // ----- a player asks the host (host: from other phones; client: to the host) -----
        void SendRollRequest();
        void SendPickRequest(int moveIndex);

        // ----- everybody reads -----
        bool TryTakeRoll(out int value);
        bool TryTakePick(out int moveIndex);
        bool TryTakeTakeover(out int player);

        // ----- the host reads requests, already checked for seat ownership -----
        // (the number is the PLAYER number in the match, 0..n-1 - not the colour of the board seat: with 2 players
        //  player 1 sits on the yellow seat, and mixing the two up made the host ignore every guest request)
        bool TryTakeRollRequest(int player);
        bool TryTakePickRequest(int player, out int moveIndex);

        /// <summary>Which players have left for good (host only): they should be given to the computer.</summary>
        bool TryTakeDeparted(out int player);

        /// <summary>Host: this player's phone lost the connection a moment ago and has 30 seconds to come back. Their turns are played for them meanwhile.</summary>
        bool IsAway(int player);

        // ----- reconnecting (a guest whose connection dropped) -----

        /// <summary>Everybody: seconds left before this player, who lost the connection, forfeits (0 = they are connected).</summary>
        float AwaySecondsLeft(int player);

        /// <summary>Guest: seconds left to get back in (0 when not reconnecting).</summary>
        float ReconnectSecondsLeft { get; }

        /// <summary>Guest: reconnecting failed because the room no longer exists, i.e. the host left. (My own network is not the problem.)</summary>
        bool HostGone { get; }

        /// <summary>Guest: try to get back into the room (keeps trying for a while). The host then sends everything that was missed.</summary>
        void StartReconnect();

        /// <summary>Guest: still trying to get back into the room.</summary>
        bool IsReconnecting { get; }

        /// <summary>Guest: the host sent the whole story of the match so far (all dice values, all picks, who was replaced by the computer).</summary>
        bool TryTakeResync(out MatchResync resync);
    }

    /// <summary>Everything a returning phone needs to catch up: the dice and picks so far, and which players the computer took over.</summary>
    public sealed class MatchResync
    {
        public int[] history = new int[0];      // 1..6 = a dice value, 10+n = "move number n was picked"
        public int[] takenOver = new int[0];    // player numbers now played by the computer
    }
}
