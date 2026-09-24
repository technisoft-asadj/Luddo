using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>
    /// The movement rules as pure functions over a GameState. Humans, the AI and the tests all use these
    /// same functions, so nobody can play by different rules. The game mode (RulesConfig.Mode) changes only what is
    /// written in GameMode's summary: Master (capture before home), Arrow (arrow slides), Blitz (one pawn home wins).
    /// </summary>
    public static class Rules
    {
        /// <summary>Fills <paramref name="moves"/> with every move the player may make with this roll.</summary>
        public static void GetLegalMoves(GameState state, RulesConfig config, int player, int roll, List<Move> moves)
        {
            moves.Clear();
            for (int t = 0; t < Board.TokensPerPlayer; t++)
            {
                int from = state.GetProgress(player, t);
                int to;
                if (Board.IsFinished(from)) continue;

                if (Board.IsInBase(from))
                {
                    if (config.SixToLeaveBase && roll != 6) continue;
                    to = Board.StartProgress;
                }
                else if (from == Board.LapProgress)
                {
                    to = roll - 1;                                  // past its own start cell: round the loop again
                }
                else
                {
                    to = from + roll;
                    bool mustGoRound = config.Mode == GameMode.Master && !state.HasCaptured(player);
                    if (Board.IsOnOuterTrack(from) && to > Board.LastOuterProgress && mustGoRound)
                    {
                        // Master mode, nobody captured yet: the home path stays shut, the pawn passes its entrance
                        to = to == Board.LastOuterProgress + 1 ? Board.LapProgress : to - Board.OuterCells;
                    }
                    else if (to > Board.FinishProgress)
                    {
                        if (config.ExactRollToFinish) continue;
                        to = Board.FinishProgress;
                    }
                }
                to = FollowArrow(state, config, player, to);
                moves.Add(new Move(player, t, from, to, roll));
            }
        }

        /// <summary>Arrow mode: a pawn stopping on an arrow cell slides to the arrow's end (never past its own home entrance).</summary>
        static int FollowArrow(GameState state, RulesConfig config, int player, int to)
        {
            if (config.Mode != GameMode.Arrow || to < Board.StartProgress || to > Board.LastOuterProgress) return to;
            if (!Board.IsArrowCell(Board.ToOuterCell(state.SeatOf(player), to))) return to;
            int end = to + Board.ArrowJump;
            return end <= Board.LastOuterProgress ? end : to;
        }

        /// <summary>
        /// Moves the token and sends captured opponent tokens back to base (added to <paramref name="captured"/>
        /// if not null). Returns true if the token reached home. The move is assumed to be legal.
        /// </summary>
        public static bool ApplyMove(GameState state, RulesConfig config, in Move move, List<TokenRef> captured)
        {
            state.SetProgress(move.Player, move.Token, move.To);

            if (Board.IsOnOuterTrack(move.To))
            {
                int cell = Board.ToOuterCell(state.SeatOf(move.Player), move.To);
                if (config.CaptureOnSafeCells || !Board.IsSafeCell(cell))
                {
                    for (int p = 0; p < state.PlayerCount; p++)
                    {
                        if (state.SameTeam(p, move.Player)) continue;      // never your own pawns, nor your partner's (TeamUp)
                        for (int t = 0; t < Board.TokensPerPlayer; t++)
                        {
                            if (Board.ToOuterCell(state.SeatOf(p), state.GetProgress(p, t)) != cell) continue;
                            state.SetProgress(p, t, Board.BaseProgress);
                            state.SetCaptured(move.Player);
                            captured?.Add(new TokenRef(p, t));
                        }
                    }
                }
            }
            return Board.IsFinished(move.To);
        }

        /// <summary>Classic rules: all four tokens home.</summary>
        public static bool HasWon(GameState state, int player)
        {
            for (int t = 0; t < Board.TokensPerPlayer; t++)
                if (!Board.IsFinished(state.GetProgress(player, t))) return false;
            return true;
        }

        /// <summary>
        /// Has this player won under the mode's rules? Blitz: one token home is enough. TeamUp: the move only wins the game
        /// once BOTH partners have all four pawns home (a lone finisher waits for their partner - see LudoGame.NextTurn,
        /// which skips a player who has nothing left to move).
        /// </summary>
        public static bool HasWon(GameState state, RulesConfig config, int player)
        {
            if (config.Mode == GameMode.Blitz)
            {
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                    if (Board.IsFinished(state.GetProgress(player, t))) return true;
                return false;
            }
            if (!state.Teams) return HasWon(state, player);
            for (int p = 0; p < state.PlayerCount; p++)
                if (state.SameTeam(p, player) && !HasWon(state, p)) return false;
            return true;
        }

        /// <summary>TeamUp: has this player brought all four of their own pawns home (their partner may still be playing)?</summary>
        public static bool IsDone(GameState state, int player) => HasWon(state, player);

        /// <summary>
        /// Every position the token passes through for this move, in order (for the animation): one step per pip, round the
        /// loop again in Master mode, and a final slide when it lands on an arrow. Leaving base = a single step onto the start cell.
        /// </summary>
        public static void Path(in Move move, List<int> steps)
        {
            steps.Clear();
            if (Board.IsInBase(move.From)) { steps.Add(move.To); return; }
            bool entersHome = Board.IsInHomeColumn(move.To) || Board.IsFinished(move.To);
            int p = move.From;
            for (int i = 0; i < move.Roll && !Board.IsFinished(p); i++)
            {
                if (p == Board.LastOuterProgress) p = entersHome ? Board.HomeColumnStart : Board.LapProgress;
                else if (p == Board.LapProgress) p = Board.StartProgress;
                else p++;
                steps.Add(p);
            }
            if (steps.Count == 0 || steps[steps.Count - 1] != move.To) steps.Add(move.To);   // the arrow slide
        }
    }
}
