using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ludo.Game;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Turns the Game scene's dice into the 3D dice: material (Ludo/DiceShaded + the face atlas from
    /// Prototype/make_dice_texture.py), a "Body" child with the model (made by DiceMesh at run time) and a soft "Shadow".
    /// Safe to run again. Menu: Ludo > Setup 3D Dice (also run by Build Game HUD).
    /// </summary>
    public static class DiceSetup
    {
        const string Folder = "Assets/_Game/Art/Dice/";
        const string TexturePath = Folder + "dice_faces.png";
        const string MaterialPath = Folder + "Dice.mat";
        const string ShaderName = "Ludo/DiceShaded";
        const string ShadowSprite = "Assets/_Game/Art/UI/Generated/shadow_soft.png";

        const string LibraryPath = "Assets/_Game/Resources/DiceSkinLibrary.asset";

        /// <summary>
        /// (Re)builds the dice collection library from the atlases Prototype/make_dice_texture.py writes: one entry per
        /// design in Ludo.Core.DiceSkins, pointing at dice_faces.png (Classic) or dice_faces_&lt;id&gt;.png. Run it after adding
        /// a design. Also run by Setup 3D Dice.
        /// </summary>
        [MenuItem("Ludo/Setup Dice Skins")]
        public static void SetupSkins()
        {
            var library = AssetDatabase.LoadAssetAtPath<DiceSkinLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<DiceSkinLibrary>();
                System.IO.Directory.CreateDirectory("Assets/_Game/Resources");
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            var all = Ludo.Core.DiceSkins.All;
            var entries = new System.Collections.Generic.List<DiceSkinLibrary.Entry>();
            foreach (var skin in all)
            {
                string path = skin.Id == Ludo.Core.DiceSkins.Default ? TexturePath : Folder + "dice_faces_" + skin.Id + ".png";
                var texture = ImportFaces(path);
                if (texture == null) { Debug.LogWarning("[Ludo] Dice design '" + skin.Id + "' has no picture at " + path + " (run Prototype/make_dice_texture.py)."); continue; }
                entries.Add(new DiceSkinLibrary.Entry { id = skin.Id, faces = texture });
            }
            library.skins = entries.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ludo] Dice designs ready: " + entries.Count + " of " + all.Length + ".");
        }

        [MenuItem("Ludo/Setup 3D Dice")]
        public static void SetupInOpenScene()
        {
            var dice = Object.FindFirstObjectByType<DiceView>();
            if (dice == null) { Debug.LogError("[Ludo] No DiceView in the open scene (open Game.unity)."); return; }
            Apply(dice);
            SetupSkins();
            EditorSceneManager.MarkSceneDirty(dice.gameObject.scene);
            EditorSceneManager.SaveScene(dice.gameObject.scene);
            Debug.Log("[Ludo] 3D dice set up.");
        }

        public static void Apply(DiceView dice)
        {
            var material = EnsureMaterial();
            var root = dice.gameObject;
            root.transform.localScale = Vector3.one;                  // sizes are set in world units by DiceView
            root.transform.rotation = Quaternion.identity;
            var oldSprite = root.GetComponent<SpriteRenderer>();       // the flat sprite dice it replaces
            if (oldSprite != null) Object.DestroyImmediate(oldSprite);

            var body = Child(root.transform, "Body");
            var filter = Get<MeshFilter>(body.gameObject);
            filter.sharedMesh = null;                                  // made at run time (DiceMesh)
            var renderer = Get<MeshRenderer>(body.gameObject);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sortingOrder = 100;

            var shadowT = Child(root.transform, "Shadow");
            var shadow = Get<SpriteRenderer>(shadowT.gameObject);
            shadow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowSprite);
            shadow.color = new Color(0f, 0f, 0.08f, 0.5f);
            shadow.sortingOrder = 99;
            float spriteWidth = shadow.sprite != null ? shadow.sprite.bounds.size.x : 1f;
            shadowT.localScale = Vector3.one * (1.6f * 1.45f / Mathf.Max(0.01f, spriteWidth));   // a little wider than the dice in the tray

            var old = root.transform.Find("SeatIcon");                          // an earlier version's sprite overlay: no longer used
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var so = new SerializedObject(dice);
            so.FindProperty("body").objectReferenceValue = renderer;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.FindProperty("trayEdge").floatValue = 1.6f;
            var board = Object.FindFirstObjectByType<BoardView>();
            so.FindProperty("boardCentre").objectReferenceValue = board != null ? board.transform : null;
            so.ApplyModifiedProperties();
        }

        static T Get<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Imports one face atlas with the settings the dice shader needs, and returns it (null = no such file).</summary>
        static Texture2D ImportFaces(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material EnsureMaterial()
        {
            ImportFaces(TexturePath);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName)) { name = "Dice" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = Shader.Find(ShaderName);
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
