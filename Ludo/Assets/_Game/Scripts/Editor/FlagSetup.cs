using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Ludo.Game;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Makes Resources/FlagLibrary.asset from the flag atlas (Art/Flags/flags_atlas.png + .txt, written by
    /// Prototype/make_flag_atlas.py). Menu: Ludo > Build Country Flags. The scene builders call Ensure().
    /// </summary>
    public static class FlagSetup
    {
        const string Folder = "Assets/_Game/Art/Flags/";
        const string AtlasPath = Folder + "flags_atlas.png";
        const string ListPath = Folder + "flags_atlas.txt";
        public const string LibraryPath = "Assets/_Game/Resources/FlagLibrary.asset";

        public static FlagLibrary Ensure() => AssetDatabase.LoadAssetAtPath<FlagLibrary>(LibraryPath) ?? Build();

        [MenuItem("Ludo/Build Country Flags")]
        public static FlagLibrary Build()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;       // a resized atlas would no longer match the rectangles
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            var codes = new List<string>();
            var rects = new List<RectInt>();
            foreach (var line in File.ReadAllLines(ListPath))
            {
                var p = line.Split(' ');
                if (p.Length != 5) continue;
                codes.Add(p[0]);
                rects.Add(new RectInt(int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4])));
            }

            var lib = AssetDatabase.LoadAssetAtPath<FlagLibrary>(LibraryPath);
            if (lib == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                lib = ScriptableObject.CreateInstance<FlagLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            lib.codes = codes.ToArray();
            lib.rects = rects.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ludo] Country flags: " + codes.Count + " flags in " + LibraryPath);
            return lib;
        }
    }
}
