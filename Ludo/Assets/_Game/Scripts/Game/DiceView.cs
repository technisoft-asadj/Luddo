using System.Collections;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// The 3D dice. It rests in its tray below the board (tap it to roll); a roll throws it onto the board, where it tumbles,
    /// bounces off the table and the invisible walls, slows down and settles - then it slides back to the tray.
    ///
    /// It can only SHOW a value it is given. The number is decided before the throw by the game engine (online: by the host,
    /// and every phone receives the same number). The throw is a real PhysX simulation, run instantly in a physics scene of its
    /// own (DiceSimulator) and then played back; when it has settled, the dice is "relabelled" - turned by one of the cube's own
    /// symmetries, which does not change how it lies - so the given number is the one on top. Physics therefore decides only
    /// how the dice moves, never the result. A throw that ends leaning on an edge is thrown again (never shown); after several
    /// failed throws a scripted tumble is used, so a roll can never get stuck.
    /// </summary>
    public sealed class DiceView : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] MeshRenderer body;                // the dice model (DiceMesh), child of this object
        [SerializeField] SpriteRenderer shadow;             // soft shadow under the dice
        // "waiting to roll": the "1" face is temporarily repointed at the pawn glyph (DiceMesh.PawnCell) and the whole
        // die tinted that player's colour - a real face of the die, the same way a number is, not a separate sprite on top of it.
        [SerializeField] float trayEdge = 1.6f;             // edge length while resting in the tray (world units)
        [SerializeField] float chamfer = 0.13f;
        [SerializeField] Vector3 trayTilt = new Vector3(-18f, 16f, 0f);   // resting pose: turned a little so it reads as 3D
        [SerializeField] Transform boardCentre;              // the middle of the board (the world origin if empty)
        [SerializeField] int rollingOrder = 3000;            // above the pawns while it rolls on the board
        [SerializeField] int trayOrder = 100;

        [Header("Throw (board cells, seconds)")]
        [SerializeField] DicePhysicsSettings physics = new DicePhysicsSettings();
        [SerializeField] float startDistance = 4.3f;         // how far from the centre (towards the roller's corner) the throw starts
        [SerializeField] float startHeight = 2.4f;
        [SerializeField] Vector2 throwSpeed = new Vector2(6.5f, 8.5f);
        [SerializeField] Vector2 spinSpeed = new Vector2(14f, 24f);   // radians per second
        [SerializeField] float spreadDegrees = 22f;          // random change of direction, so no two throws look the same
        [SerializeField] float heightScale = 0.09f;          // a dice higher above the board is drawn a little bigger
        [SerializeField] float settlePause = 0.35f;          // the number stays visible on the board this long
        [SerializeField] float returnSeconds = 0.32f;        // then the dice slides back to the tray
        [SerializeField] float scriptedSeconds = 1.1f;       // length of the fallback tumble
        [SerializeField] Vector2 shakeSeconds = new Vector2(0.55f, 0.85f);   // suspense: shaken in the tray before it is thrown

        // physics space (Y up, board = XZ plane) -> world (board in XY, the camera looks along +Z)
        static readonly Quaternion ToWorld = Quaternion.Euler(-90f, 0f, 0f);
        static readonly Vector2[] SeatCorner = { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) };   // Red, Green, Yellow, Blue

        readonly DiceThrowRecord record = new DiceThrowRecord();
        Transform bodyT;
        Vector3 baseShadowScale;
        int shownValue = 1;
        bool ready;
        bool rolling;
        bool pawnFaceActive;                                // true while the "1" face shows the pawn glyph instead of its pip
        MeshFilter bodyFilter;
        Material bodyMaterial;

        public bool IsRolling => rolling;

        void Awake()
        {
            bodyT = body.transform;
            bodyFilter = body.GetComponent<MeshFilter>();
            bodyFilter.sharedMesh = DiceMesh.Build(chamfer);
            bodyMaterial = body.material;                       // an instance copy: safe to tint without touching the shared asset
            if (shadow != null) baseShadowScale = shadow.transform.localScale;
            Rest(shownValue);
        }

        void OnDestroy() => DiceSimulator.Release();

        void LateUpdate()
        {
            if (rolling) return;
            // the tray follows this object (GameController keeps it next to the player whose turn it is)
            float pulse = ready ? 1f + 0.07f * Mathf.Sin(Time.time * 6f) : 1f;      // "tap me"
            bodyT.position = transform.position;
            bodyT.localScale = Vector3.one * trayEdge * pulse;
            PlaceShadow(transform.position, 0f, trayEdge * pulse);
        }

        /// <summary>Show a value at rest in the tray (also cancels a roll that was cut short: a restart, a reconnect).</summary>
        public void Show(int value)
        {
            if (rolling) AudioService.Stop(SfxId.DiceRoll);
            Rest(value);
        }

        /// <summary>
        /// A new player's turn has begun and they have not rolled yet: cover the dice with their own pawn colour (like Ludo
        /// Star's coloured dice cup) instead of leaving the last number showing. Cleared automatically once they roll.
        /// </summary>
        public void ShowSeatIcon(int seat)
        {
            if (!pawnFaceActive)
            {
                DiceMesh.SetFaceCell(bodyFilter.sharedMesh, 1, DiceMesh.PawnCell);
                pawnFaceActive = true;
            }
            if (bodyMaterial != null) bodyMaterial.color = seat >= 0 && seat < 4 ? SeatStyle.Colors[seat] : Color.white;
            if (!rolling) bodyT.rotation = TrayRotation(1);         // the pawn glyph lives on face "1": always show that face while waiting
        }

        void HideSeatIcon()
        {
            if (pawnFaceActive)
            {
                DiceMesh.SetFaceCell(bodyFilter.sharedMesh, 1, 0);  // "1"'s own pip back - the physics relabelling needs every face correct
                pawnFaceActive = false;
            }
            if (bodyMaterial != null) bodyMaterial.color = Color.white;
        }

        /// <summary>Pulse while waiting for the player to tap the dice.</summary>
        public void SetReady(bool on) => ready = on;

        public IEnumerator PlayRoll(int finalValue) => PlayRoll(finalValue, -1);

        /// <summary>Throw the dice from the rolling player's corner ('seat', -1 = any) and let it settle showing 'finalValue'.</summary>
        public IEnumerator PlayRoll(int finalValue, int seat)
        {
            ready = false;
            rolling = true;
            HideSeatIcon();
            finalValue = Mathf.Clamp(finalValue, 1, 6);
            Plan(seat);
            Quaternion relabel = DiceMesh.Relabel(finalValue, record.upAxis);
            SetOrder(rollingOrder);

            // suspense: shake it in the tray first, like rattling a dice cup, before it is thrown onto the board
            yield return Shake(Random.Range(shakeSeconds.x, shakeSeconds.y));

            // play the recorded throw back (game time: pausing the game pauses the dice)
            float t = 0f, duration = record.Duration;
            while (t < duration)
            {
                t += Time.deltaTime;
                ApplyFrame(Mathf.Min(t, duration), relabel);
                yield return null;
            }
            ApplyFrame(duration, relabel);
            AudioService.Stop(SfxId.DiceRoll);
            AudioService.Play(SfxId.DiceLand);
            shownValue = finalValue;

            yield return new WaitForSeconds(settlePause);

            // slide back to the tray, turning the number towards the player
            Vector3 fromPos = bodyT.position;
            Quaternion fromRot = bodyT.rotation;
            float fromScale = bodyT.localScale.x;
            Quaternion toRot = TrayRotation(finalValue);
            for (float k = 0f; k < 1f;)
            {
                k = Mathf.Min(1f, k + Time.deltaTime / Mathf.Max(0.01f, returnSeconds));
                float e = 1f - (1f - k) * (1f - k);
                bodyT.SetPositionAndRotation(Vector3.Lerp(fromPos, transform.position, e), Quaternion.Slerp(fromRot, toRot, e));
                float scale = Mathf.Lerp(fromScale, trayEdge, e);
                bodyT.localScale = Vector3.one * scale;
                PlaceShadow(bodyT.position, 0f, scale);
                yield return null;
            }
            Rest(finalValue);
            bodyT.localScale = Vector3.one * trayEdge * 1.12f;                   // a small "thump" in the tray
            yield return new WaitForSeconds(0.07f);
        }

        // ---------- the throw ----------

        /// <summary>Rattle the dice in place before the throw, building suspense (does not touch the physics result).</summary>
        IEnumerator Shake(float seconds)
        {
            Vector3 basePos = transform.position;
            AudioService.Play(SfxId.DiceRoll);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float intensity = Mathf.Lerp(0.05f, 0.16f, k);          // builds up towards the throw
                Vector3 jitter = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * intensity;
                bodyT.position = basePos + jitter;
                bodyT.rotation = Quaternion.Euler(Random.Range(-40f, 40f), Random.Range(-40f, 40f), Random.Range(-180f, 180f));
                float scale = trayEdge * (1f + 0.06f * Mathf.Sin(t * 46f));
                bodyT.localScale = Vector3.one * scale;
                PlaceShadow(bodyT.position, 0f, scale);
                yield return null;
            }
            bodyT.position = basePos;
        }

        /// <summary>Record a throw: real physics first, the scripted tumble only if physics keeps ending badly.</summary>
        void Plan(int seat)
        {
            Vector2 corner = seat >= 0 && seat < SeatCorner.Length ? SeatCorner[seat] : SeatCorner[Random.Range(0, 4)];
            Vector3 dir = new Vector3(corner.x, 0f, corner.y).normalized;
            for (int attempt = 0; attempt < Mathf.Max(1, physics.maxAttempts); attempt++)
            {
                Vector3 start = dir * startDistance + new Vector3(Random.Range(-0.6f, 0.6f), startHeight + Random.Range(-0.3f, 0.4f), Random.Range(-0.6f, 0.6f));
                Vector3 aim = Quaternion.Euler(0f, Random.Range(-spreadDegrees, spreadDegrees), 0f) * -dir;
                Vector3 velocity = aim * Random.Range(throwSpeed.x, throwSpeed.y) + Vector3.up * Random.Range(0.5f, 2.2f);
                Vector3 spin = Random.onUnitSphere * Random.Range(spinSpeed.x, spinSpeed.y);
                if (DiceSimulator.Throw(physics, start, Random.rotationUniform, velocity, spin, record)) return;
            }
            Debug.LogWarning("[Ludo] Dice: physics gave no clean landing, using the scripted roll");
            ScriptedTumble(dir);
        }

        /// <summary>The fallback: a hand-made tumble from the roller's corner to the middle, ending flat. Always valid.</summary>
        void ScriptedTumble(Vector3 dir)
        {
            record.Clear();
            record.step = 1f / 60f;
            record.scripted = true;
            float half = physics.edge * 0.5f;
            Vector3 from = dir * startDistance + Vector3.up * startHeight;
            Vector3 to = dir * Random.Range(-1f, 1f) + Vector3.Cross(dir, Vector3.up) * Random.Range(-1.5f, 1.5f) + Vector3.up * half;
            Quaternion start = Random.rotationUniform;
            Quaternion end = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);          // flat, +Y (axis 2) on top
            Vector3 spinAxis = Random.onUnitSphere;
            int frames = Mathf.CeilToInt(scriptedSeconds / record.step);
            for (int i = 0; i <= frames; i++)
            {
                float k = (float)i / frames;
                float ease = 1f - (1f - k) * (1f - k);
                Vector3 p = Vector3.Lerp(from, to, ease);
                p.y = Mathf.Lerp(from.y, half, ease) + Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k) * 1.2f;   // bounces that die away
                record.positions.Add(p);
                record.rotations.Add(Quaternion.AngleAxis(720f * (1f - ease), spinAxis) * Quaternion.Slerp(start, end, ease));
            }
            record.upAxis = 2;
        }

        void ApplyFrame(float time, Quaternion relabel)
        {
            float f = time / record.step;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, record.positions.Count - 1);
            int j = Mathf.Min(i + 1, record.positions.Count - 1);
            float k = Mathf.Clamp01(f - i);
            Vector3 p = Vector3.Lerp(record.positions[i], record.positions[j], k);
            Quaternion r = Quaternion.Slerp(record.rotations[i], record.rotations[j], k);
            float height = Mathf.Max(0f, p.y - physics.edge * 0.5f);
            Vector3 centre = boardCentre != null ? boardCentre.position : Vector3.zero;
            bodyT.SetPositionAndRotation(centre + ToWorld * p, ToWorld * r * relabel);
            float scale = physics.edge * (1f + heightScale * height);
            bodyT.localScale = Vector3.one * scale;
            PlaceShadow(centre + ToWorld * new Vector3(p.x, 0f, p.z), height, scale);
        }

        // ---------- resting in the tray ----------

        void Rest(int value)
        {
            rolling = false;
            HideSeatIcon();
            shownValue = Mathf.Clamp(value, 1, 6);
            SetOrder(trayOrder);
            bodyT.SetPositionAndRotation(transform.position, TrayRotation(shownValue));
            bodyT.localScale = Vector3.one * trayEdge;
            PlaceShadow(transform.position, 0f, trayEdge);
        }

        /// <summary>'value' facing the player (the camera looks along +Z), turned a little so two more sides show.</summary>
        Quaternion TrayRotation(int value) =>
            Quaternion.Euler(trayTilt) * Quaternion.FromToRotation(DiceMesh.FaceNormal[value - 1], Vector3.back);

        void PlaceShadow(Vector3 onBoard, float height, float size)
        {
            if (shadow == null) return;
            shadow.transform.position = onBoard + new Vector3(0.12f + 0.16f * height, -0.2f - 0.22f * height, 0.5f);
            shadow.transform.rotation = Quaternion.identity;
            shadow.transform.localScale = baseShadowScale * (size / Mathf.Max(0.01f, trayEdge)) * (1f + 0.12f * height);
            var c = shadow.color;
            c.a = Mathf.Lerp(0.5f, 0.18f, Mathf.Clamp01(height / 3f));
            shadow.color = c;
        }

        void SetOrder(int order)
        {
            body.sortingOrder = order;
            if (shadow != null) shadow.sortingOrder = order - 1;
        }
    }
}
