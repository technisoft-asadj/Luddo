using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ludo.Core;

namespace Ludo.Game
{
    /// <summary>
    /// Shows a LudoGame on the board: creates the 4 tokens of every player, puts them where the game state says,
    /// and animates every move it hears about. It only LISTENS to the game (Moved event) - it never changes it.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] TokenView tokenPrefab;
        [SerializeField] Sprite tokenSprite;                     // one white pawn, tinted per seat
        [SerializeField] Sprite pawnOutline;                     // untinted layers drawn with every pawn
        [SerializeField] Sprite pawnGloss;
        [SerializeField] Sprite pawnShadow;
        [SerializeField] Transform tokenRoot;
        [SerializeField] float cellSize = 1f;                    // world units per board cell
        [SerializeField] Vector2 tokenOffset = new Vector2(0f, 0.18f); // lifts the pawn so its foot stands on the cell
        [SerializeField] float secondsPerStep = 0.12f;
        [SerializeField] float captureHopSeconds = 0.45f;

        LudoGame game;
        TokenView[,] tokens;                       // [player, token]
        readonly Queue<MoveResult> pending = new Queue<MoveResult>();
        Coroutine runner;

        public bool IsAnimating => runner != null;

        public void Bind(LudoGame newGame)
        {
            if (game != null) game.Moved -= OnMoved;
            StopAllCoroutines();
            runner = null;
            pending.Clear();
            if (tokens != null) foreach (var t in tokens) if (t != null) Destroy(t.gameObject);

            game = newGame;
            var state = game.State;
            tokens = new TokenView[state.PlayerCount, Board.TokensPerPlayer];
            for (int p = 0; p < state.PlayerCount; p++)
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                {
                    var view = Instantiate(tokenPrefab, tokenRoot);
                    view.name = "Token P" + p + "-" + t;
                    view.Init(p, t, tokenSprite, SeatStyle.Colors[state.SeatOf(p)], pawnShadow, pawnOutline, pawnGloss);
                    tokens[p, t] = view;
                }
            SnapToState();
            game.Moved += OnMoved;
            ShowArrows(game.Mode == GameMode.Arrow);
        }

        // ---------- Arrow mode: the four arrows drawn on the board ----------

        readonly List<GameObject> arrows = new List<GameObject>();
        static Sprite arrowSprite;

        /// <summary>Arrow mode: an arrow from each arrow cell (2 after a start cell) to the star cell it leads to.</summary>
        void ShowArrows(bool on)
        {
            foreach (var a in arrows) if (a != null) Destroy(a);
            arrows.Clear();
            if (!on) return;
            if (arrowSprite == null) arrowSprite = MakeArrowSprite();
            for (int seat = 0; seat < Board.MaxPlayers; seat++)
            {
                int fromCell = Board.StartCell(seat) + Board.ArrowOffset;
                Vector3 a = CellCentre(BoardGrid.OuterCell(fromCell));
                Vector3 b = CellCentre(BoardGrid.OuterCell(fromCell + Board.ArrowJump));
                var go = new GameObject("Arrow" + seat);
                go.transform.SetParent(transform, false);
                go.transform.position = (a + b) * 0.5f;
                Vector3 d = b - a;
                go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                float length = d.magnitude + 0.5f * cellSize;
                go.transform.localScale = new Vector3(length / 2.56f, 0.62f * cellSize / 0.64f, 1f);   // sprite is 2.56 x 0.64 units
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = arrowSprite;
                sr.color = new Color(1f, 1f, 1f, 0.82f);
                sr.sortingOrder = 5;                                   // on the board, under the pawns
                var outline = new GameObject("Shade");
                outline.transform.SetParent(go.transform, false);
                outline.transform.localPosition = new Vector3(0.02f, -0.03f, 0f);
                outline.transform.localScale = new Vector3(1.03f, 1.18f, 1f);
                var so = outline.AddComponent<SpriteRenderer>();
                so.sprite = arrowSprite;
                so.color = new Color(0.05f, 0.12f, 0.35f, 0.55f);
                so.sortingOrder = 4;
                arrows.Add(go);
            }
        }

        Vector3 CellCentre(GridPoint p) =>
            transform.position + new Vector3((p.X - BoardGrid.Size * 0.5f) * cellSize, (BoardGrid.Size * 0.5f - p.Y) * cellSize, 0f);

        /// <summary>A plain white arrow (shaft + head), drawn in code: 256 x 64 px, pointing right.</summary>
        static Sprite MakeArrowSprite()
        {
            const int w = 256, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cy = Mathf.Abs(y + 0.5f - h * 0.5f);
                    float head = 70f;                                      // the head's length in pixels
                    float inside;
                    if (x < w - head) inside = Mathf.Min(h * 0.17f - cy, x + 0.5f - 6f);                       // the shaft
                    else inside = (w - x - 0.5f) * (h * 0.5f / head) - cy;                                        // the head (a triangle)
                    float a = Mathf.Clamp01(inside + 0.5f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        void OnDestroy()
        {
            if (game != null) game.Moved -= OnMoved;
        }

        // ---------- selecting tokens ----------

        public Color SeatColor(int seat) => SeatStyle.Colors[seat];

        public Vector3 TokenWorldPosition(int player, int token) => tokens[player, token].transform.position;

        /// <summary>Make the tokens of these moves pulse (or stop pulsing).</summary>
        public void SetSelectable(IReadOnlyList<Move> moves, bool on)
        {
            for (int i = 0; i < moves.Count; i++) tokens[moves[i].Player, moves[i].Token].SetSelectable(on);
        }

        /// <summary>Which of the legal moves did the player tap? The nearest token within the radius wins.</summary>
        public bool TryPick(Vector3 world, IReadOnlyList<Move> moves, float radius, out Move picked)
        {
            picked = default;
            float best = radius;
            bool found = false;
            for (int i = 0; i < moves.Count; i++)
            {
                float d = Vector2.Distance(world, tokens[moves[i].Player, moves[i].Token].transform.position);
                if (d > best) continue;
                best = d;
                picked = moves[i];
                found = true;
            }
            return found;
        }

        // ---------- positions ----------

        Vector3 ToWorld(GridPoint p) =>
            transform.position + new Vector3((p.X - BoardGrid.Size * 0.5f) * cellSize + tokenOffset.x,
                                             (BoardGrid.Size * 0.5f - p.Y) * cellSize + tokenOffset.y, 0f);

        Vector3 WorldFor(int player, int token, int progress) =>
            ToWorld(BoardGrid.ForProgress(game.State.SeatOf(player), token, progress));

        /// <summary>Put every token exactly where the game state says. Tokens sharing a spot are spread out.</summary>
        public void SnapToState()
        {
            var state = game.State;
            var groups = new Dictionary<Vector2Int, List<TokenView>>();
            for (int p = 0; p < state.PlayerCount; p++)
                for (int t = 0; t < Board.TokensPerPlayer; t++)
                {
                    var gp = BoardGrid.ForProgress(state.SeatOf(p), t, state.GetProgress(p, t));
                    var key = new Vector2Int(Mathf.RoundToInt(gp.X * 100f), Mathf.RoundToInt(gp.Y * 100f));
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<TokenView>(4);
                    list.Add(tokens[p, t]);
                }

            foreach (var list in groups.Values)
            {
                int n = list.Count;
                float scale = StackLayout.Scale(n);
                for (int i = 0; i < n; i++)
                {
                    var v = list[i];
                    Vector3 pos = WorldFor(v.Player, v.Token, state.GetProgress(v.Player, v.Token));
                    Vector2 spread = StackLayout.Offset(i, n) * cellSize;   // a tidy grid of stacked tokens
                    pos.x += spread.x;
                    pos.y += spread.y;
                    v.Place(pos, scale);
                }
            }
        }

        // ---------- animation ----------

        void OnMoved(MoveResult result)
        {
            pending.Enqueue(result);
            if (runner == null) runner = StartCoroutine(Run());
        }

        readonly List<int> steps = new List<int>();

        IEnumerator Run()
        {
            while (pending.Count > 0)
            {
                var r = pending.Dequeue();
                var token = tokens[r.Move.Player, r.Move.Token];

                // one waypoint per progress step (leaving base = a single step onto the start cell)
                var path = new List<Vector3>();
                Rules.Path(r.Move, steps);                     // round the loop again (Master), arrow slides (Arrow)
                foreach (int p in steps) path.Add(WorldFor(r.Move.Player, r.Move.Token, p));
                yield return token.MoveAlong(path, secondsPerStep);

                if (r.ReachedHome && r.Captured.Length == 0)
                {
                    AudioService.Play(SfxId.Home);
                    Haptics.Pulse(40);
                }

                // captured tokens hop back to their base
                if (r.Captured.Length > 0)
                {
                    AudioService.Play(SfxId.Capture);
                    Haptics.Pulse(70);
                }
                foreach (var c in r.Captured)
                    yield return tokens[c.Player, c.Token].Hop(WorldFor(c.Player, c.Token, Board.BaseProgress), captureHopSeconds);

                SnapToState();   // tidy up stacks after every move
            }
            runner = null;
        }
    }
}
