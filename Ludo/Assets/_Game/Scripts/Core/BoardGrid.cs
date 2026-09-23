using System;

namespace Ludo.Core
{
    /// <summary>A point on the board in "cell units": x to the right, y downwards, origin = top-left corner
    /// of the 15x15 board. The centre of cell (col,row) is (col + 0.5, row + 0.5).</summary>
    public readonly struct GridPoint
    {
        public readonly float X;
        public readonly float Y;
        public GridPoint(float x, float y) { X = x; Y = y; }
    }

    /// <summary>
    /// Where every progress value sits on the standard 15x15 Ludo board (Red top-left, Green top-right,
    /// Yellow bottom-right, Blue bottom-left, clockwise = seat order). Pure numbers - no Unity, no sprites:
    /// the view multiplies these by the size of a cell to get screen/world positions.
    /// </summary>
    public static class BoardGrid
    {
        public const int Size = 15;
        const float Centre = 7.5f;

        // The 52 cells of the shared loop, clockwise, cell 0 = Red's start. (col,row) pairs.
        static readonly int[] OuterCol =
        {
            1,2,3,4,5,            // 0-4    left arm, top row, heading right
            6,6,6,6,6,6,          // 5-10   top arm, left column, heading up
            7,8,                  // 11-12  top arm, top edge
            8,8,8,8,8,            // 13-17  top arm, right column, heading down
            9,10,11,12,13,14,     // 18-23  right arm, top row, heading right
            14,14,                // 24-25  right arm, right edge
            13,12,11,10,9,        // 26-30  right arm, bottom row, heading left
            8,8,8,8,8,8,          // 31-36  bottom arm, right column, heading down
            7,6,                  // 37-38  bottom arm, bottom edge
            6,6,6,6,6,            // 39-43  bottom arm, left column, heading up
            5,4,3,2,1,0,          // 44-49  left arm, bottom row, heading left
            0,0                   // 50-51  left arm, left edge
        };

        static readonly int[] OuterRow =
        {
            6,6,6,6,6,
            5,4,3,2,1,0,
            0,0,
            1,2,3,4,5,
            6,6,6,6,6,6,
            7,8,
            8,8,8,8,8,
            9,10,11,12,13,14,
            14,14,
            13,12,11,10,9,
            8,8,8,8,8,8,
            7,6
        };

        /// <summary>Centre of a cell of the shared loop (0..51).</summary>
        public static GridPoint OuterCell(int cell) => new GridPoint(OuterCol[cell] + 0.5f, OuterRow[cell] + 0.5f);

        /// <summary>Centre of a home-column cell. step 0..4 = progress 51..55 of that seat.</summary>
        public static GridPoint HomeColumn(int seat, int step)
        {
            switch (seat)
            {
                case (int)Seat.Red:    return new GridPoint(1 + step + 0.5f, 7.5f);
                case (int)Seat.Green:  return new GridPoint(7.5f, 1 + step + 0.5f);
                case (int)Seat.Yellow: return new GridPoint(13 - step + 0.5f, 7.5f);
                default:               return new GridPoint(7.5f, 13 - step + 0.5f);
            }
        }

        /// <summary>Where the four tokens of a seat rest while in base (the four circles of its corner).</summary>
        public static GridPoint BaseSlot(int seat, int token)
        {
            float left = (seat == (int)Seat.Green || seat == (int)Seat.Yellow) ? 11f : 2f;
            float top = (seat == (int)Seat.Yellow || seat == (int)Seat.Blue) ? 11f : 2f;
            return new GridPoint(left + (token % 2) * 2f, top + (token / 2) * 2f);
        }

        /// <summary>Where a finished token rests: in the centre triangle of its colour, fanned out a little.</summary>
        public static GridPoint FinishSpot(int seat, int token)
        {
            float dx = 0f, dy = 0f;
            switch (seat)
            {
                case (int)Seat.Red:    dx = -0.85f; break;
                case (int)Seat.Green:  dy = -0.85f; break;
                case (int)Seat.Yellow: dx = 0.85f; break;
                default:               dy = 0.85f; break;
            }
            float spread = (token - 1.5f) * 0.3f;
            // fan along the side of the triangle (perpendicular to the direction of travel)
            return dx != 0f ? new GridPoint(Centre + dx, Centre + spread) : new GridPoint(Centre + spread, Centre + dy);
        }

        /// <summary>Position for any progress value (-1 base ... 56 finished).</summary>
        public static GridPoint ForProgress(int seat, int token, int progress)
        {
            if (Board.IsInBase(progress)) return BaseSlot(seat, token);
            if (Board.IsFinished(progress)) return FinishSpot(seat, token);
            if (Board.IsInHomeColumn(progress)) return HomeColumn(seat, progress - Board.HomeColumnStart);
            if (Board.IsOnOuterTrack(progress)) return OuterCell(Board.ToOuterCell(seat, progress));
            throw new ArgumentOutOfRangeException(nameof(progress));
        }
    }
}
