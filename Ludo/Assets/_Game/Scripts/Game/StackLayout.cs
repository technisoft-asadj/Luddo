using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// Where each pawn goes when several pawns stand on the same cell: a tidy grid (1 = centre, 2 = side by side,
    /// 3-4 = a 2x2 block, 5-9 = 3x3 ...) and a smaller size so they still fit inside the cell. Pure maths, no scene.
    /// </summary>
    public static class StackLayout
    {
        const float Spacing = 0.27f;     // distance between neighbouring pawns, in cells

        /// <summary>Offset of pawn 'index' (0-based) out of 'count', in cells, from the middle of the cell.</summary>
        public static Vector2 Offset(int index, int count)
        {
            if (count <= 1) return Vector2.zero;

            int columns = count == 2 ? 2 : Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            int col = index % columns;
            int row = index / columns;

            // the last row may be shorter: centre it
            int inThisRow = row == rows - 1 ? count - row * columns : columns;
            float x = (col - (inThisRow - 1) * 0.5f) * Spacing;
            float y = ((rows - 1) * 0.5f - row) * Spacing * 0.8f;     // rows sit a little closer: pawns are tall
            return new Vector2(x, y);
        }

        /// <summary>Size factor for pawns that share a cell (1 when alone).</summary>
        public static float Scale(int count)
        {
            if (count <= 1) return 1f;
            if (count == 2) return 0.86f;
            if (count <= 4) return 0.74f;
            return 0.6f;
        }
    }
}
