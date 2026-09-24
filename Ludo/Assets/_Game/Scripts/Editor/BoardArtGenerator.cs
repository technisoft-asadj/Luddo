using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Draws the Ludo board (lined up with BoardGrid, 100 px per cell) and a layered pawn (shadow, white outline, shaded
    /// body tinted per seat, gloss), then puts them into the Game and Menu scenes. Menu: Ludo > Generate Board And Pawns.
    /// </summary>
    public static class BoardArtGenerator
    {
        public const string Folder = "Assets/_Game/Art/Board/";
        const int Cell = 100;
        const int BoardPx = BoardGrid.Size * Cell;
        const int PawnPx = 256;
        const float PawnPpu = 500f;                // with the Token prefab's 2.8 scale a pawn is about 1.25 cells tall: bold and clear, still inside its neighbours

        static readonly Color Gap = new Color(0.80f, 0.85f, 0.93f);
        static readonly Color Paper = Color.white;
        static readonly Color StarGrey = new Color(0.70f, 0.75f, 0.86f);

        [MenuItem("Ludo/Generate Board And Pawns")]
        public static void GenerateAndApply()
        {
            Directory.CreateDirectory(Folder);
            SaveSprite("board", DrawBoard(), BoardPx, BoardPx, 100f);
            SavePawnLayers();
            ApplyToScenes();
            Debug.Log("[Ludo] Board and pawn art generated and applied.");
        }

        // ---------- the board ----------

        static Color Pastel(Color c) => Color.Lerp(c, Color.white, 0.55f);
        static Color Deep(Color c) => Color.Lerp(c, Color.black, 0.18f);

        static Color[] DrawBoard()
        {
            var px = new Color[BoardPx * BoardPx];
            for (int i = 0; i < px.Length; i++) px[i] = Gap;

            // the 52 cells of the loop
            for (int cell = 0; cell < Board.OuterCells; cell++)
            {
                var g = BoardGrid.OuterCell(cell);
                int col = (int)g.X, row = (int)g.Y;
                int r = cell % 13, arm = cell / 13;                 // r==0 = the entrance cell, r==8 = the shared safe cell further round
                bool tinted = r == 0 || r == 8;
                // the entrance cell: the same full colour as the home column and the pawn's own ring (not the paler tint).
                // the other star cell on the same arm: a lighter tint of that colour instead of plain white, so the grey
                // star is no longer sitting on a bare white square.
                Color fill = r == 0 ? SeatStyle.Colors[arm] : r == 8 ? Pastel(SeatStyle.Colors[arm]) : Paper;
                PaintCell(px, col, row, fill);
                if (Board.IsSafeCell(cell))
                    PaintStar(px, col + 0.5f, row + 0.5f, 0.32f, tinted ? Color.white : StarGrey);
            }

            // the home columns: the same full, saturated colour as the ring round each resting well (the part of the well a
            // pawn does not cover, so it is what actually reads as "the pawn's colour" on the board) - not the paler tint
            // used only for the small circle under the pawn itself
            for (int seat = 0; seat < 4; seat++)
                for (int step = 0; step < 5; step++)
                {
                    var g = BoardGrid.HomeColumn(seat, step);
                    PaintCell(px, (int)g.X, (int)g.Y, SeatStyle.Colors[seat]);
                }

            // the four yards
            PaintYard(px, 0, 0, 0);
            PaintYard(px, 9, 0, 1);
            PaintYard(px, 9, 9, 2);
            PaintYard(px, 0, 9, 3);

            PaintCentre(px);
            return px;
        }

        static void PaintCell(Color[] px, int col, int row, Color fill)
        {
            float x0 = col * Cell, y0 = row * Cell;
            Fill(px, x0, y0, Cell, Cell, (x, y) => RoundRectCoverage(x - x0, y - y0, Cell, Cell, 4f, 12f), fill);
        }

        static void PaintYard(Color[] px, int col, int row, int seat)
        {
            Color c = SeatStyle.Colors[seat];
            float x0 = col * Cell, y0 = row * Cell, size = 6 * Cell;
            // rich colour with a soft top-to-bottom light
            Fill(px, x0, y0, size, size, (x, y) => RoundRectCoverage(x - x0, y - y0, size, size, 4f, 26f),
                 (x, y) => Color.Lerp(Color.Lerp(c, Color.white, 0.14f), Deep(c), (y - y0) / size));
            // white inner panel (the owner asked for this back: colour the home columns instead, not the yard)
            Fill(px, x0, y0, size, size, (x, y) => RoundRectCoverage(x - x0, y - y0, size, size, 72f, 34f), Paper);
            // the four resting wells, lined up with BoardGrid.BaseSlot
            for (int t = 0; t < 4; t++)
            {
                var s = BoardGrid.BaseSlot(seat, t);
                float cx = s.X * Cell, cy = s.Y * Cell;
                Fill(px, cx - 60, cy - 60, 120, 120, (x, y) => CircleCoverage(x, y, cx, cy, 46f), c);
                Fill(px, cx - 60, cy - 60, 120, 120, (x, y) => CircleCoverage(x, y, cx, cy, 37f), Pastel(c));
            }
        }

        static void PaintCentre(Color[] px)
        {
            float x0 = 6 * Cell, y0 = 6 * Cell, size = 3 * Cell, cx = x0 + size * 0.5f, cy = y0 + size * 0.5f;
            Fill(px, x0, y0, size, size, (x, y) => 1f, (x, y) =>
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                int seat = Mathf.Abs(dx) > Mathf.Abs(dy) ? (dx < 0 ? 0 : 2) : (dy < 0 ? 1 : 3);
                Color c = SeatStyle.Colors[seat];
                float edge = Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy));                    // distance to the diagonals
                float light = Mathf.Clamp01(new Vector2(dx, dy).magnitude / (size * 0.7f));
                Color body = Color.Lerp(Color.Lerp(c, Color.white, 0.12f), Deep(c), light);
                return Color.Lerp(Color.white, body, Mathf.Clamp01(edge / 3f - 0.4f));   // thin white seams on the diagonals
            });
            Fill(px, cx - 50, cy - 50, 100, 100, (x, y) => CircleCoverage(x, y, cx, cy, 36f), Color.white);
            PaintStar(px, cx / Cell, cy / Cell, 0.27f, new Color(1f, 0.78f, 0.15f));
        }

        static void PaintStar(Color[] px, float cxCells, float cyCells, float radiusCells, Color color)
        {
            float cx = cxCells * Cell, cy = cyCells * Cell, r = radiusCells * Cell;
            Fill(px, cx - r - 2, cy - r - 2, 2 * r + 4, 2 * r + 4, (x, y) => StarCoverage(x + 0.5f - cx, y + 0.5f - cy, r), color);
        }

        // ---------- the pawn (four layers on the same canvas, so they line up) ----------

        static void SavePawnLayers()
        {
            var body = new Color[PawnPx * PawnPx];
            var outline = new Color[PawnPx * PawnPx];
            var gloss = new Color[PawnPx * PawnPx];
            var shadow = new Color[PawnPx * PawnPx];
            var ink = new Color(0.06f, 0.09f, 0.24f);

            for (int y = 0; y < PawnPx; y++)
                for (int x = 0; x < PawnPx; x++)
                {
                    int i = y * PawnPx + x;
                    float fx = x + 0.5f, fy = y + 0.5f;           // y up (texture space)
                    float d = PawnSdf(fx, fy);

                    // body: white with grey shading, so the seat tint gives a lit, rounded pawn
                    float a = Mathf.Clamp01(0.5f - d);
                    float rim = Mathf.Clamp01(-d / 16f);
                    float side = Mathf.Clamp01((fx - 128f) / 80f);
                    float v = Mathf.Lerp(0.66f, 1f, rim) * (1f - 0.22f * side);
                    if (fy < 60f) v *= 0.85f;                      // the base is a little darker than the body
                    body[i] = new Color(v, v, v, a);

                    // outline: a white band with a thin dark edge, drawn behind the body
                    float oa = Mathf.Clamp01(0.5f - (d - 9f));
                    Color oc = Color.Lerp(Color.white, ink, Mathf.Clamp01((d - 6f) / 1.5f));
                    outline[i] = new Color(oc.r, oc.g, oc.b, oa);

                    // gloss: a soft white highlight on the head
                    float g1 = Soft(Ellipse(fx, fy, 112f, 204f, 15f, 10f), 7f) * 0.6f;
                    gloss[i] = new Color(1f, 1f, 1f, g1 * a);

                    // soft shadow on the ground
                    float s = Soft(Ellipse(fx, fy, 136f, 30f, 66f, 14f), 16f) * 0.38f;
                    shadow[i] = new Color(0f, 0f, 0.08f, s);
                }

            SaveSprite("pawn_body", body, PawnPx, PawnPx, PawnPpu);
            SaveSprite("pawn_outline", outline, PawnPx, PawnPx, PawnPpu);
            SaveSprite("pawn_gloss", gloss, PawnPx, PawnPx, PawnPpu);
            SaveSprite("pawn_shadow", shadow, PawnPx, PawnPx, PawnPpu);
        }

        /// <summary>Signed distance (pixels, negative inside) to the pawn: base disc, bell body, collar and head.</summary>
        static float PawnSdf(float x, float y)
        {
            float baseD = EllipseSdf(x, y, 128f, 50f, 58f, 17f);
            float t = Mathf.Clamp01((y - 50f) / 100f);
            float half = Mathf.Lerp(52f, 25f, Mathf.Pow(t, 0.75f));
            float dx = Mathf.Abs(x - 128f) - half, dy = Mathf.Max(50f - y, y - 150f);
            float bodyD = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f);
            float collar = EllipseSdf(x, y, 128f, 150f, 40f, 12f);
            float head = new Vector2(x - 128f, y - 190f).magnitude - 42f;
            return Mathf.Min(Mathf.Min(baseD, bodyD), Mathf.Min(collar, head));
        }

        // ---------- shape helpers ----------

        static float EllipseSdf(float x, float y, float cx, float cy, float rx, float ry) =>
            (new Vector2((x - cx) / rx, (y - cy) / ry).magnitude - 1f) * Mathf.Min(rx, ry);

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry) => EllipseSdf(x, y, cx, cy, rx, ry);

        static float Soft(float d, float blur) => 1f - Mathf.SmoothStep(-blur, blur, d);

        static float CircleCoverage(float x, float y, float cx, float cy, float r) =>
            Mathf.Clamp01(0.5f - (new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude - r));

        static float RoundRectCoverage(float x, float y, float w, float h, float inset, float radius)
        {
            float hx = w * 0.5f - inset, hy = h * 0.5f - inset;
            float qx = Mathf.Abs(x + 0.5f - w * 0.5f) - (hx - radius);
            float qy = Mathf.Abs(y + 0.5f - h * 0.5f) - (hy - radius);
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            return Mathf.Clamp01(0.5f - d);
        }

        /// <summary>A five-pointed star, point up (board rows run downwards, so "up" is negative y), 4x supersampled.</summary>
        static float StarCoverage(float x, float y, float r)
        {
            int hits = 0;
            for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (InStar(x + (sx - 1.5f) * 0.25f, -(y + (sy - 1.5f) * 0.25f), r)) hits++;
            return hits / 16f;
        }

        static bool InStar(float x, float y, float r)
        {
            bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                Vector2 a = StarVertex(i, r), b = StarVertex(j, r);
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        static Vector2 StarVertex(int i, float r)
        {
            float radius = i % 2 == 0 ? r : r * 0.45f;
            float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        // ---------- painting into the board buffer (board rows run downwards; textures run upwards) ----------

        delegate float Cover(float x, float y);
        delegate Color Paint(float x, float y);

        static void Fill(Color[] px, float x0, float y0, float w, float h, Cover cover, Color color) =>
            Fill(px, x0, y0, w, h, cover, (x, y) => color);

        static void Fill(Color[] px, float x0, float y0, float w, float h, Cover cover, Paint paint)
        {
            int xa = Mathf.Max(0, Mathf.FloorToInt(x0)), xb = Mathf.Min(BoardPx, Mathf.CeilToInt(x0 + w));
            int ya = Mathf.Max(0, Mathf.FloorToInt(y0)), yb = Mathf.Min(BoardPx, Mathf.CeilToInt(y0 + h));
            for (int y = ya; y < yb; y++)
                for (int x = xa; x < xb; x++)
                {
                    float a = cover(x, y);
                    if (a <= 0f) continue;
                    int i = (BoardPx - 1 - y) * BoardPx + x;
                    Color c = paint(x, y);
                    px[i] = Color.Lerp(px[i], new Color(c.r, c.g, c.b, 1f), a * c.a);
                }
        }

        // ---------- saving and wiring ----------

        static void SaveSprite(string name, Color[] pixels, int w, int h, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            string path = Folder + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = name.StartsWith("pawn");     // pawns are drawn small: mipmaps keep their edges smooth
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + name + ".png");

        static void ApplyToScenes()
        {
            string previous = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.SaveOpenScenes();

            var game = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Game.unity", OpenSceneMode.Single);
            foreach (var root in game.GetRootGameObjects())
            {
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.gameObject.name == "Board")
                    {
                        sr.sprite = Load("board");
                        sr.transform.localScale = Vector3.one;          // 1500 px at 100 ppu = the 15 cells of the grid
                        EditorUtility.SetDirty(sr);
                        EditorUtility.SetDirty(sr.transform);
                    }
                foreach (var view in root.GetComponentsInChildren<BoardView>(true))
                {
                    var so = new SerializedObject(view);
                    so.FindProperty("tokenSprite").objectReferenceValue = Load("pawn_body");
                    so.FindProperty("pawnOutline").objectReferenceValue = Load("pawn_outline");
                    so.FindProperty("pawnGloss").objectReferenceValue = Load("pawn_gloss");
                    so.FindProperty("pawnShadow").objectReferenceValue = Load("pawn_shadow");
                    so.FindProperty("tokenOffset").vector2Value = new Vector2(0f, 0.1f);    // lifts the pawn a little so its base sits on the cell
                    so.ApplyModifiedProperties();
                }
            }
            EditorSceneManager.SaveScene(game);

            var menu = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Menu.unity", OpenSceneMode.Single);
            foreach (var root in menu.GetRootGameObjects())
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                    if (img.gameObject.name == "BoardPicture")
                    {
                        img.sprite = Load("board");
                        EditorUtility.SetDirty(img);
                    }
            EditorSceneManager.SaveScene(menu);

            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
    }
}
