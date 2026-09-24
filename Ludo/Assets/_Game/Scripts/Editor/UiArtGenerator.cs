using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Makes the small technical building blocks the polished UI is assembled from: white rounded shapes (tinted in
    /// code to any colour), soft shadows, gradients, glows and sunburst rays, plus an outlined text material.
    /// They are plain white shapes, not artwork: all colour comes from the Image tint, so one sprite serves every colour.
    /// Run from the menu Ludo > Generate UI Shapes.
    /// </summary>
    public static class UiArtGenerator
    {
        public const string Folder = "Assets/_Game/Art/UI/Generated/";
        public const string OutlineMaterialPath = "Assets/_Game/Art/Fonts/LilitaOne Outline.mat";
        const string FontPath = "Assets/_Game/Art/Fonts/LilitaOne SDF.asset";

        [MenuItem("Ludo/Generate UI Shapes")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            Save("panel_round", 96, 96, 100f, 32, (x, y, w, h) => RoundRect(x, y, w, h, 30f, 0f));
            Save("panel_world", 160, 160, 100f, 66, (x, y, w, h) => RoundRect(x, y, w, h, 64f, 0f));
            Save("ring_round", 96, 96, 100f, 32, (x, y, w, h) => RoundRect(x, y, w, h, 30f, 7f));
            Save("shadow_soft", 128, 128, 100f, 60, (x, y, w, h) => SoftRect(x, y, w, h, 34f, 20f, 30f));
            Save("circle", 128, 128, 100f, 0, (x, y, w, h) => Circle(x, y, w, h));
            Save("gradient_v", 4, 256, 100f, 0, (x, y, w, h) => Mathf.SmoothStep(0f, 1f, y / (h - 1f)));   // opaque at the top, clear at the bottom
            Save("glow_radial", 256, 256, 100f, 0, (x, y, w, h) => Glow(x, y, w, h));
            Save("rays", 512, 512, 100f, 0, (x, y, w, h) => Rays(x, y, w, h, 16));
            Save("dice_white", 128, 128, 100f, 0, (x, y, w, h) => DiceIcon(x, y, w, h));   // the dice collection tile
            Save("chest_white", 128, 128, 100f, 0, (x, y, w, h) => ChestIcon(x, y, w, h));  // the free-chest button
            MakeWhiteCopy("Assets/ThirdParty/GoogleMaterial/Icons/ai_robot_black.png", "ai_robot_white");
            // online icons (Google Material Icons): white copies so they can be tinted like the other icons
            foreach (var n in new[] { "mic", "mic_off", "share", "copy", "person_add", "block", "flag", "group", "public", "flash_on", "chat", "person", "search" })
                MakeWhiteCopy("Assets/ThirdParty/GoogleMaterial/Icons/" + n + "_black.png", n + "_white");
            ImportIconFolder();
            MakeOutlineMaterial();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Ludo] UI shapes generated in " + Folder);
        }

        /// <summary>
        /// The white icon silhouettes drawn by Prototype/make_ui_icons.py (crown, gem, star, chevron, ...). They are files
        /// on disk rather than shapes built here because a crown is far easier to describe as polygons than as per-pixel
        /// maths; this only makes sure Unity imports them the same way as the generated shapes.
        /// </summary>
        public const string IconFolder = "Assets/_Game/Art/UI/Icons/";

        static void ImportIconFolder()
        {
            if (!Directory.Exists(IconFolder)) return;
            foreach (var path in Directory.GetFiles(IconFolder, "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        // ---------- shapes (each returns the alpha 0..1 of one pixel) ----------

        /// <summary>Rounded rectangle; ring > 0 keeps only a border of that many pixels.</summary>
        static float RoundRect(float x, float y, float w, float h, float radius, float ring)
        {
            float outer = Coverage(Sdf(x + 0.5f, y + 0.5f, w, h, 0f, radius));
            if (ring <= 0f) return outer;
            float inner = Coverage(Sdf(x + 0.5f, y + 0.5f, w, h, ring, Mathf.Max(1f, radius - ring)));
            return Mathf.Clamp01(outer - inner);
        }

        /// <summary>Signed distance to a rounded rectangle inset by 'inset' pixels (negative inside).</summary>
        static float Sdf(float px, float py, float w, float h, float inset, float radius)
        {
            float hx = w * 0.5f - inset, hy = h * 0.5f - inset;
            float qx = Mathf.Abs(px - w * 0.5f) - (hx - radius);
            float qy = Mathf.Abs(py - h * 0.5f) - (hy - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);   // 1 px anti-aliased edge

        static float SoftRect(float x, float y, float w, float h, float inset, float radius, float blur)
        {
            float d = Sdf(x + 0.5f, y + 0.5f, w, h, inset, radius);
            return 1f - Mathf.SmoothStep(-blur * 0.5f, blur * 0.5f, d);
        }

        /// <summary>A dice icon: a rounded square with five pips punched out of it (used on the dice collection tile).</summary>
        static float DiceIcon(float x, float y, float w, float h)
        {
            float body = Coverage(Sdf(x + 0.5f, y + 0.5f, w, h, w * 0.08f, w * 0.20f));
            float pip = 0f;
            float r = w * 0.085f;
            var spots = new[]
            {
                new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.30f), new Vector2(0.50f, 0.50f),
                new Vector2(0.30f, 0.70f), new Vector2(0.70f, 0.70f)
            };
            foreach (var spot in spots)
            {
                float d = new Vector2(x + 0.5f - spot.x * w, y + 0.5f - spot.y * h).magnitude - r;
                pip = Mathf.Max(pip, Coverage(d));
            }
            return Mathf.Clamp01(body - pip);
        }

        /// <summary>A treasure chest: a rounded body with the lid line and the keyhole punched out of it.</summary>
        static float ChestIcon(float x, float y, float w, float h)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float body = Coverage(Sdf(px, py, w, h, w * 0.14f, w * 0.14f));
            float lidLine = Coverage(Mathf.Abs(py - h * 0.56f) - h * 0.035f) * Coverage(Mathf.Abs(px - w * 0.5f) - w * 0.36f);
            float hole = Coverage(new Vector2(px - w * 0.5f, py - h * 0.56f).magnitude - w * 0.075f);
            return Mathf.Clamp01(body - Mathf.Max(lidLine, hole));
        }

        static float Circle(float x, float y, float w, float h)
        {
            float d = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f).magnitude - (w * 0.5f - 1f);
            return Coverage(d);
        }

        static float Glow(float x, float y, float w, float h)
        {
            float r = new Vector2((x + 0.5f) / w - 0.5f, (y + 0.5f) / h - 0.5f).magnitude * 2f;
            float a = Mathf.Clamp01(1f - r);
            return a * a * (3f - 2f * a);
        }

        static float Rays(float x, float y, float w, float h, int count)
        {
            float dx = (x + 0.5f) / w - 0.5f, dy = (y + 0.5f) / h - 0.5f;
            float r = new Vector2(dx, dy).magnitude * 2f;
            float angle = Mathf.Atan2(dy, dx);
            float wave = Mathf.Sin(angle * count) * 0.5f + 0.5f;
            float ray = Mathf.SmoothStep(0.35f, 0.85f, wave);
            float fade = Mathf.Clamp01(1f - r);
            return ray * fade * Mathf.Clamp01(r * 6f);
        }

        // ---------- writing the PNG and setting the import options ----------

        delegate float Shape(float x, float y, float w, float h);

        static void Save(string name, int width, int height, float ppu, int border, Shape shape)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(shape(x, y, width, height)) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            string path = Folder + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;   // gradients must not band
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // required for stretchable (sliced) sprites
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        /// <summary>
        /// The robot icon is black artwork, and a tint can only darken, so it cannot be shown white on a coloured badge.
        /// This writes a copy with every pixel white and the original transparency, i.e. the same picture recoloured.
        /// </summary>
        static void MakeWhiteCopy(string sourcePath, string newName)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(sourcePath));
            var pixels = tex.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, pixels[i].a);
            tex.SetPixels32(pixels);
            tex.Apply();
            string path = Folder + newName + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        // ---------- outlined text ----------

        /// <summary>A copy of the Lilita One material with a dark outline and a soft drop shadow, for titles and button labels.</summary>
        static void MakeOutlineMaterial()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) { Debug.LogError("[Ludo] Font asset missing: " + FontPath); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (mat == null)
            {
                mat = new Material(font.material) { name = "LilitaOne Outline" };
                AssetDatabase.CreateAsset(mat, OutlineMaterialPath);
            }
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", 0.22f);
            mat.SetColor("_OutlineColor", new Color(0.05f, 0.10f, 0.32f, 1f));
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0.1f, 0.45f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.75f);
            mat.SetFloat("_UnderlayDilate", 0.3f);
            mat.SetFloat("_UnderlaySoftness", 0.35f);
            EditorUtility.SetDirty(mat);
        }
    }
}
