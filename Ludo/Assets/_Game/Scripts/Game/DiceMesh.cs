using System.Collections.Generic;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// The dice model, made in code: a cube of edge 1 with bevelled (chamfered) edges and corners, like a real dice. Each of the
    /// six faces shows its own picture from the face atlas (4 x 2 cells: the pictures of 1..6, then a plain cell used for the
    /// bevels). Opposite faces add up to 7, as on a real dice.
    /// </summary>
    public static class DiceMesh
    {
        /// <summary>The side of the model that shows each value (index = value - 1). Opposite sides add up to 7.</summary>
        public static readonly Vector3[] FaceNormal =
        {
            Vector3.up,        // 1
            Vector3.right,     // 2
            Vector3.forward,   // 3
            Vector3.back,      // 4
            Vector3.left,      // 5
            Vector3.down       // 6
        };

        /// <summary>The direction the picture's top points to on the given face (the same 'v' axis Build() uses).</summary>
        public static Vector3 FaceUp(int value)
        {
            Vector3 n = FaceNormal[value - 1];
            Vector3 u = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
            return Vector3.Cross(n, u);
        }

        const int Columns = 4, Rows = 2;
        const int PlainCell = 6;

        /// <summary>The unused 8th atlas cell: a pawn silhouette, drawn the same way as the pips. Waiting for a roll, the "1"
        /// face is temporarily repointed here (see SetFaceCell) and the whole die is tinted the current player's colour.</summary>
        public const int PawnCell = 7;

        const int Grid = 14;                                   // vertices per side of one face
        const int FaceVertices = Grid * Grid;

        /// <summary>
        /// A rounded cube of edge 1, like the glossy dice in the reference: every face is a grid of vertices pushed onto a
        /// rounded box (radius = 1.5 x chamfer) with smooth normals, so light rolls over the corners instead of catching flat
        /// bevels. Each face keeps its own atlas picture; the curved rim shows the picture's shaded border. Face 'value' owns
        /// vertices (value-1) x FaceVertices ..+FaceVertices-1 (used by SetFaceCell).
        /// </summary>
        public static Mesh Build(float chamfer = 0.12f)
        {
            float h = 0.5f;
            float radius = Mathf.Clamp(chamfer * 1.5f, 0.05f, 0.3f);
            float inner = h - radius;
            var verts = new List<Vector3>(FaceVertices * 6);
            var normals = new List<Vector3>(FaceVertices * 6);
            var uvs = new List<Vector2>(FaceVertices * 6);
            var tris = new List<int>(6 * (Grid - 1) * (Grid - 1) * 6);

            for (int value = 1; value <= 6; value++)
            {
                Vector3 n = FaceNormal[value - 1];
                Vector3 u = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
                Vector3 v = Vector3.Cross(n, u);             // u x v = n: the picture reads upright from outside
                int start = verts.Count;
                for (int gy = 0; gy < Grid; gy++)
                    for (int gx = 0; gx < Grid; gx++)
                    {
                        float a = Mathf.Lerp(-h, h, gx / (float)(Grid - 1));
                        float b = Mathf.Lerp(-h, h, gy / (float)(Grid - 1));
                        Vector3 p = n * h + u * a + v * b;                                   // the point on the plain cube
                        Vector3 q = new Vector3(Mathf.Clamp(p.x, -inner, inner), Mathf.Clamp(p.y, -inner, inner), Mathf.Clamp(p.z, -inner, inner));
                        Vector3 d = p - q;
                        Vector3 normal = d.sqrMagnitude < 1e-8f ? n : d.normalized;
                        verts.Add(q + normal * radius);
                        normals.Add(normal);
                        uvs.Add(CellUv(value - 1, new Vector2(gx / (float)(Grid - 1), gy / (float)(Grid - 1))));
                    }
                for (int gy = 0; gy < Grid - 1; gy++)
                    for (int gx = 0; gx < Grid - 1; gx++)
                    {
                        int i0 = start + gy * Grid + gx, i1 = i0 + 1, i2 = i0 + Grid + 1, i3 = i0 + Grid;
                        // u x v = n, so (i0, i1, i2) runs counter-clockwise seen from outside
                        Vector3 cross = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);
                        bool facesOut = Vector3.Dot(cross, normals[i0]) > 0f;
                        if (facesOut) tris.AddRange(new[] { i0, i1, i2, i0, i2, i3 });
                        else tris.AddRange(new[] { i0, i2, i1, i0, i3, i2 });
                    }
            }

            var mesh = new Mesh { name = "Dice" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector2 CellUv(int cell, Vector2 local)
        {
            int col = cell % Columns, row = cell / Columns;
            const float inset = 0.004f;
            float x = (col + Mathf.Lerp(inset * Columns, 1f - inset * Columns, local.x)) / Columns;
            float y = 1f - (row + 1f - Mathf.Lerp(inset * Rows, 1f - inset * Rows, local.y)) / Rows;
            return new Vector2(x, y);
        }

        static Vector2 CellCentre(int cell) => CellUv(cell, new Vector2(0.5f, 0.5f));

        static readonly Vector2[] QuadCorners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };

        /// <summary>
        /// Repoint one baked face's picture at a different atlas cell (built() lays the six faces out first, four vertices each,
        /// in face order, so face 'value' always owns vertices (value-1)*4 .. +3). Used to show the pawn cell on the "1" face
        /// while waiting to roll, and to put "1"'s own pips back before any real roll (the physics relabelling depends on every
        /// face still showing its own number).
        /// </summary>
        public static void SetFaceCell(Mesh mesh, int value, int cell)
        {
            var uvs = new List<Vector2>();
            mesh.GetUVs(0, uvs);
            int start = (value - 1) * FaceVertices;
            // every picture sits at the same place inside its cell, so moving to another cell is one shift for all vertices
            Vector2 from = CellOrigin(CellOf(uvs[start]));
            Vector2 shift = CellOrigin(cell) - from;
            for (int i = 0; i < FaceVertices; i++) uvs[start + i] += shift;
            mesh.SetUVs(0, uvs);
        }

        static int CellOf(Vector2 uv)
        {
            int col = Mathf.Clamp(Mathf.FloorToInt(uv.x * Columns), 0, Columns - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt((1f - uv.y) * Rows), 0, Rows - 1);
            return row * Columns + col;
        }

        static Vector2 CellOrigin(int cell)
        {
            int col = cell % Columns, row = cell / Columns;
            return new Vector2(col / (float)Columns, 1f - (row + 1f) / Rows);
        }

        static void AddQuad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3[] q, Vector3 normal, Vector2[] quv)
        {
            int start = v.Count;
            for (int i = 0; i < 4; i++) { v.Add(q[i]); n.Add(normal); uv.Add(quv[i]); }
            bool facesOut = Vector3.Dot(Vector3.Cross(q[1] - q[0], q[2] - q[0]), normal) > 0f;
            if (facesOut) t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            else t.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }

        static void AddTriangle(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3[] p, Vector3 normal, Vector2 plain)
        {
            int start = v.Count;
            for (int i = 0; i < 3; i++) { v.Add(p[i]); n.Add(normal); uv.Add(plain); }
            bool facesOut = Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), normal) > 0f;
            if (facesOut) t.AddRange(new[] { start, start + 1, start + 2 });
            else t.AddRange(new[] { start, start + 2, start + 1 });
        }

        /// <summary>
        /// The turn that relabels the dice so 'value' is on the side that physics left on top ('upAxis', see DiceSimulator.Axes).
        /// It is one of the cube's own 24 symmetries, so the dice looks and lies exactly the same - only the numbers move.
        /// </summary>
        public static Quaternion Relabel(int value, int upAxis)
        {
            Vector3 from = FaceNormal[Mathf.Clamp(value, 1, 6) - 1];
            Vector3 to = DiceSimulator.Axes[upAxis];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    for (int z = 0; z < 4; z++)
                    {
                        var r = Quaternion.Euler(x * 90f, y * 90f, z * 90f);
                        if (Vector3.Dot(r * from, to) > 0.99f) return r;
                    }
            return Quaternion.identity;                  // (unreachable: every side can be turned onto every other)
        }
    }
}
