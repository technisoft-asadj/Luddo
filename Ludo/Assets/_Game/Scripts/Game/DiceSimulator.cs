using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ludo.Game
{
    /// <summary>Everything the dice throw can be tuned with (all in board cells and seconds).</summary>
    [System.Serializable]
    public sealed class DicePhysicsSettings
    {
        [Tooltip("Edge length of the dice on the board, in board cells.")] public float edge = 1.45f;
        [Tooltip("Half size of the square the dice may roll in (invisible walls), from the board centre.")] public float arenaHalf = 5.4f;
        public float gravity = 40f;
        public float mass = 1f;
        [Range(0f, 1f)] public float bounciness = 0.3f;
        [Range(0f, 1f)] public float friction = 0.5f;
        public float linearDamping = 0.2f;
        public float angularDamping = 0.8f;
        [Tooltip("A throw that has not come to rest after this long is thrown again (never shown).")] public float maxSeconds = 3.2f;
        public float settleSpeed = 0.06f;
        public float settleAngularSpeed = 0.12f;
        [Tooltip("How flat the dice must lie at the end (1 = perfectly flat). A dice leaning on an edge is thrown again.")] public float flatDot = 0.985f;
        [Tooltip("Throws tried before the scripted fallback roll is used.")] public int maxAttempts = 6;
    }

    /// <summary>One recorded throw: where the dice was every physics step, and which of its sides ended up on top.</summary>
    public sealed class DiceThrowRecord
    {
        public readonly List<Vector3> positions = new List<Vector3>();       // physics space: Y is up, the board is the XZ plane
        public readonly List<Quaternion> rotations = new List<Quaternion>();
        public float step;
        public int upAxis;                                                    // index into DiceSimulator.Axes
        public bool scripted;                                                 // the fallback roll (physics did not give a clean result)
        public float Duration => Mathf.Max(0, positions.Count - 1) * step;
        public void Clear() { positions.Clear(); rotations.Clear(); upAxis = 2; scripted = false; }
    }

    /// <summary>
    /// A real PhysX throw of the dice, run instantly in a separate physics scene of its own (a floor, four invisible walls and
    /// the dice - nothing else), and recorded step by step so DiceView can play it back smoothly on the board. Because the
    /// simulation lives in its own scene it can never touch the tokens, the UI or anything else, and it never decides the
    /// number: the game engine has already decided it, and DiceView only relabels the dice so that number ends on top.
    /// </summary>
    public static class DiceSimulator
    {
        /// <summary>The dice's six side directions (in the dice's own space).</summary>
        public static readonly Vector3[] Axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        const float Step = 1f / 60f;

        static Scene scene;
        static PhysicsScene physics;
        static Rigidbody body;
        static BoxCollider bodyBox;
        static PhysicsMaterial material;
        static readonly BoxCollider[] walls = new BoxCollider[5];            // floor + 4 walls
        static float builtArena = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            scene = default;
            body = null;
            bodyBox = null;
            material = null;
            builtArena = -1f;
        }

        /// <summary>Throw the dice once. False = it did not come to rest flat inside the arena in time (the caller throws again).</summary>
        public static bool Throw(DicePhysicsSettings s, Vector3 start, Quaternion startRotation, Vector3 velocity, Vector3 spin, DiceThrowRecord record)
        {
            record.Clear();
            record.step = Step;
            Ensure(s);
            float half = s.edge * 0.5f;
            bodyBox.size = Vector3.one * s.edge;
            material.bounciness = s.bounciness;
            material.dynamicFriction = s.friction;
            material.staticFriction = s.friction;
            body.mass = s.mass;
            body.linearDamping = s.linearDamping;
            body.angularDamping = s.angularDamping;
            body.maxAngularVelocity = 60f;
            body.position = start;
            body.rotation = startRotation;
            body.transform.SetPositionAndRotation(start, startRotation);
            body.linearVelocity = velocity;
            body.angularVelocity = spin;
            body.WakeUp();
            Physics.SyncTransforms();

            int maxSteps = Mathf.CeilToInt(s.maxSeconds / Step);
            int still = 0;
            record.positions.Add(start);
            record.rotations.Add(startRotation);
            for (int i = 0; i < maxSteps; i++)
            {
                body.AddForce(Vector3.down * s.gravity, ForceMode.Acceleration);    // own gravity: the game's global physics settings stay untouched
                physics.Simulate(Step);
                record.positions.Add(body.position);
                record.rotations.Add(body.rotation);
                bool resting = body.linearVelocity.magnitude < s.settleSpeed && body.angularVelocity.magnitude < s.settleAngularSpeed && body.position.y < half * 1.2f;
                still = resting || body.IsSleeping() ? still + 1 : 0;
                if (still >= 8) break;
            }
            if (still < 8) return false;                                        // still moving: too long a roll

            Vector3 p = body.position;
            if (Mathf.Abs(p.x) > s.arenaHalf || Mathf.Abs(p.z) > s.arenaHalf || p.y < 0f) return false;   // (cannot happen with the walls, but never trust it)
            record.upAxis = UpAxis(body.rotation, out float flat);
            return flat >= s.flatDot;                                           // leaning on an edge: no clear top side
        }

        /// <summary>Which of the dice's sides points up for this rotation, and how straight up (1 = exactly).</summary>
        public static int UpAxis(Quaternion rotation, out float dot)
        {
            int best = 0;
            dot = -2f;
            for (int i = 0; i < Axes.Length; i++)
            {
                float d = Vector3.Dot(rotation * Axes[i], Vector3.up);
                if (d > dot) { dot = d; best = i; }
            }
            return best;
        }

        static void Ensure(DicePhysicsSettings s)
        {
            if (!scene.IsValid() || !scene.isLoaded || body == null)
            {
                scene = SceneManager.CreateScene("DiceThrow", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                physics = scene.GetPhysicsScene();
                material = new PhysicsMaterial("Dice") { bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Average };
                for (int i = 0; i < walls.Length; i++)
                {
                    var w = new GameObject(i == 0 ? "Floor" : "Wall" + i);
                    SceneManager.MoveGameObjectToScene(w, scene);
                    walls[i] = w.AddComponent<BoxCollider>();
                    walls[i].sharedMaterial = material;
                }
                var dice = new GameObject("Dice");
                SceneManager.MoveGameObjectToScene(dice, scene);
                bodyBox = dice.AddComponent<BoxCollider>();
                bodyBox.sharedMaterial = material;
                body = dice.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // fast throws never pass through a wall
                builtArena = -1f;
            }
            if (!Mathf.Approximately(builtArena, s.arenaHalf))
            {
                builtArena = s.arenaHalf;
                float a = s.arenaHalf, thick = 4f, high = 30f;
                Place(walls[0], new Vector3(0f, -thick * 0.5f, 0f), new Vector3(a * 2f + thick * 2f, thick, a * 2f + thick * 2f));
                Place(walls[1], new Vector3(a + thick * 0.5f, high * 0.5f, 0f), new Vector3(thick, high, a * 2f + thick * 2f));
                Place(walls[2], new Vector3(-a - thick * 0.5f, high * 0.5f, 0f), new Vector3(thick, high, a * 2f + thick * 2f));
                Place(walls[3], new Vector3(0f, high * 0.5f, a + thick * 0.5f), new Vector3(a * 2f + thick * 2f, high, thick));
                Place(walls[4], new Vector3(0f, high * 0.5f, -a - thick * 0.5f), new Vector3(a * 2f + thick * 2f, high, thick));
            }
        }

        static void Place(BoxCollider box, Vector3 centre, Vector3 size)
        {
            box.transform.position = centre;
            box.size = size;
        }

        /// <summary>Free the physics scene (the game scene is closing).</summary>
        public static void Release()
        {
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            scene = default;
            body = null;
        }
    }
}
