using UnityEditor;
using UnityEngine;
using Ludo.Game;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Renders every collectable dice design as a picture: the real 3D dice model (the one thrown on the board), with that
    /// design's face atlas, seen from above at an angle so three faces show, on a transparent background. The collection
    /// screen, the profile and the chest use these so the dice in the menus look like the dice in the game, not like a flat
    /// square with dots. Menu: Ludo > Render Dice Icons (Setup Dice Skins runs it too).
    /// </summary>
    public static class DiceIcons
    {
        public const string Folder = "Assets/_Game/Art/Dice/Icons/";
        const int Size = 256;

        [MenuItem("Ludo/Render Dice Icons")]
        public static void RenderAll()
        {
            System.IO.Directory.CreateDirectory(Folder);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Dice/Dice.mat");
            if (material == null) { Debug.LogError("[Ludo] Dice.mat missing (run Setup 3D Dice)."); return; }
            var mesh = DiceMesh.Build(0.12f);
            var preview = new PreviewRenderUtility();
            try
            {
                var cam = preview.camera;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = 0.98f;
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
                cam.transform.position = new Vector3(0f, 0.9f, -3f);
                cam.transform.rotation = Quaternion.Euler(16f, 0f, 0f);
                preview.lights[0].intensity = 1.0f;
                preview.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                preview.lights[1].intensity = 0.5f;
                preview.ambientColor = new Color(0.55f, 0.55f, 0.6f);

                foreach (var skin in Ludo.Core.DiceSkins.All)
                {
                    var faces = DiceSkinLibrary.Faces(skin.Id);
                    if (faces == null) continue;
                    var mat = new Material(material);
                    mat.SetTexture("_MainTex", faces);
                    // turn the model so the "5" (its left face) is on top, then tip it so two more faces show
                    var turn = Quaternion.Euler(0f, -32f, 0f) * Quaternion.Euler(-24f, 0f, 0f) * Quaternion.FromToRotation(Vector3.left, Vector3.up);
                    // render on black and on white: the difference gives the true transparency (soft edges included)
                    var onBlack = Shoot(preview, cam, mesh, mat, turn, Color.black);
                    var onWhite = Shoot(preview, cam, mesh, mat, turn, Color.white);
                    var png = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                    var pb = onBlack; var pw = onWhite;
                    for (int i = 0; i < pb.Length; i++)
                    {
                        float a = Mathf.Clamp01(1f - (pw[i].r - pb[i].r + pw[i].g - pb[i].g + pw[i].b - pb[i].b) / 3f);
                        Color c = a > 0.001f ? new Color(pb[i].r / a, pb[i].g / a, pb[i].b / a, a) : new Color(0f, 0f, 0f, 0f);
                        c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
                        pb[i] = c;
                    }
                    png.SetPixels(pb);
                    png.Apply();
                    System.IO.File.WriteAllBytes(Folder + "dice_icon_" + skin.Id + ".png", png.EncodeToPNG());
                    Object.DestroyImmediate(png);
                    Object.DestroyImmediate(mat);
                }
            }
            finally { preview.Cleanup(); }
            AssetDatabase.Refresh();
            foreach (var skin in Ludo.Core.DiceSkins.All)
            {
                string p = Folder + "dice_icon_" + skin.Id + ".png";
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            Debug.Log("[Ludo] Dice icons rendered.");
        }

        static Color[] Shoot(PreviewRenderUtility preview, Camera cam, Mesh mesh, Material mat, Quaternion turn, Color background)
        {
            cam.backgroundColor = background;
            preview.BeginStaticPreview(new Rect(0, 0, Size, Size));
            preview.DrawMesh(mesh, Matrix4x4.TRS(Vector3.zero, turn, Vector3.one * 1.1f), mat, 0);
            cam.Render();
            var tex = preview.EndStaticPreview();
            return tex.GetPixels();
        }
    }
}
