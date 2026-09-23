using System.IO;
using UnityEditor;
using UnityEngine;
using Ludo.Game;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Imports the animal faces as sprites and creates Resources/AvatarLibrary.asset (the list of choosable pictures,
    /// plus the robot used for computer players). The ORDER below decides the default avatar of Player 1, 2, 3, 4.
    /// Safe to run again.
    /// </summary>
    public static class AvatarSetup
    {
        const string Folder = "Assets/ThirdParty/Kenney/Avatars/";
        const string RobotPath = "Assets/ThirdParty/GoogleMaterial/Icons/ai_robot_black.png";
        const string LibraryFolder = "Assets/_Game/Resources";
        public const string LibraryPath = LibraryFolder + "/AvatarLibrary.asset";

        static readonly string[] Order =
            { "panda", "rabbit", "pig", "penguin", "monkey", "parrot", "elephant", "giraffe", "hippo", "snake" };

        [MenuItem("Ludo/Setup Avatar Library")]
        public static void Run()
        {
            var sprites = new Sprite[Order.Length];
            for (int i = 0; i < Order.Length; i++)
            {
                string path = Folder + Order[i] + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { Debug.LogError("[Ludo] Missing avatar image: " + path); continue; }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            Directory.CreateDirectory(LibraryFolder);
            var lib = AssetDatabase.LoadAssetAtPath<AvatarLibrary>(LibraryPath);
            bool isNew = lib == null;
            if (isNew) lib = ScriptableObject.CreateInstance<AvatarLibrary>();
            lib.avatars = sprites;
            lib.computer = AssetDatabase.LoadAssetAtPath<Sprite>(RobotPath);
            if (isNew) AssetDatabase.CreateAsset(lib, LibraryPath);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ludo] Avatar library ready: " + sprites.Length + " avatars, robot=" + (lib.computer != null));
        }
    }
}
