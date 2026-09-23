using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using Ludo.Core;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Turns the Noto Emoji pictures (Assets/ThirdParty/NotoEmoji) into what the chat needs: every picture imported as a UI
    /// sprite (for the emoji buttons) and one TextMeshPro sprite asset (atlas + one sprite per emoji, looked up by name and by
    /// Unicode) so emojis show inside chat text. Menu: Ludo > Build Chat Emojis.
    /// </summary>
    public static class EmojiSetup
    {
        const string Source = "Assets/ThirdParty/NotoEmoji/";
        const string Folder = "Assets/_Game/Art/Emoji/";
        public const string SpriteAssetPath = Folder + "ChatEmojis.asset";
        const int Cell = 128, Columns = 6;

        public static string SourcePath(int code) => Source + "emoji_u" + ChatEmoji.Name(code) + ".png";

        public static Sprite ButtonSprite(int code) => AssetDatabase.LoadAssetAtPath<Sprite>(SourcePath(code));

        /// <summary>The sprite asset, built only if it does not exist yet (rebuilding would break scenes that already use it).</summary>
        public static TMP_SpriteAsset Ensure() =>
            AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath) ?? Build();

        [MenuItem("Ludo/Build Chat Emojis")]
        public static TMP_SpriteAsset Build()
        {
            Directory.CreateDirectory(Folder);
            var codes = ChatEmoji.Codes;

            // 1. every picture as a UI sprite
            foreach (int code in codes)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(SourcePath(code));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            // 2. one atlas for the text sprites
            int rows = (codes.Length + Columns - 1) / Columns;
            var atlas = new Texture2D(Columns * Cell, rows * Cell, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            for (int i = 0; i < codes.Length; i++)
            {
                var pic = new Texture2D(2, 2);
                pic.LoadImage(File.ReadAllBytes(SourcePath(codes[i])));
                int x = (i % Columns) * Cell, y = atlas.height - (i / Columns + 1) * Cell;
                atlas.SetPixels(x, y, Cell, Cell, pic.GetPixels());
                Object.DestroyImmediate(pic);
            }
            atlas.Apply();
            string atlasPath = Folder + "chat_emoji_atlas.png";
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
            var ai = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            ai.textureType = TextureImporterType.Default;
            ai.npotScale = TextureImporterNPOTScale.None;      // a resized atlas would no longer match the sprite rectangles
            ai.alphaIsTransparency = true;
            ai.mipmapEnabled = true;
            ai.textureCompression = TextureImporterCompression.CompressedHQ;
            ai.SaveAndReimport();
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            // 3. the TextMeshPro sprite asset
            AssetDatabase.DeleteAsset(SpriteAssetPath);
            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            AssetDatabase.CreateAsset(asset, SpriteAssetPath);
            asset.spriteSheet = sheet;
            var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "ChatEmojis Material" };
            material.SetTexture(ShaderUtilities.ID_MainTex, sheet);
            AssetDatabase.AddObjectToAsset(material, asset);
            asset.material = material;
            asset.faceInfo = new FaceInfo { pointSize = Cell, scale = 1f, ascentLine = Cell * 0.8f, descentLine = -Cell * 0.2f, lineHeight = Cell };
            asset.spriteInfoList = new System.Collections.Generic.List<TMP_Sprite>();
            asset.UpdateLookupTables();                // a new asset has no version yet: this runs TMP's one-time upgrade on the empty tables first

            for (int i = 0; i < codes.Length; i++)
            {
                int x = (i % Columns) * Cell, y = (rows - 1 - i / Columns) * Cell;
                var glyph = new TMP_SpriteGlyph
                {
                    index = (uint)i,
                    metrics = new GlyphMetrics(Cell, Cell, 0f, Cell * 0.84f, Cell),
                    glyphRect = new GlyphRect(x, y, Cell, Cell),
                    scale = 1f,
                    atlasIndex = 0
                };
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter((uint)codes[i], glyph) { name = ChatEmoji.Name(codes[i]), scale = 1f });
            }
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ludo] Chat emojis built: " + codes.Length + " emojis in " + SpriteAssetPath);
            return asset;
        }
    }
}
