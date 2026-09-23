using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ludo.Game
{
    /// <summary>
    /// The player's own photo (from their Google / Facebook profile or picked from the phone's gallery) and the photos
    /// of other online players. Photos are stored small (128 x 128, JPEG) so they are cheap to keep on the phone and to
    /// share; on screen they are cut into a circle so they fit the round picture frames the animal avatars use.
    /// A player with no photo simply keeps the animal avatar they chose.
    /// </summary>
    public static class ProfilePhoto
    {
        public const int Size = 128;
        const int JpegQuality = 80;

        static Sprite mine;
        static bool loaded;
        static readonly Dictionary<string, Sprite> others = new Dictionary<string, Sprite>();

        /// <summary>Goes up every time my photo is set or removed (the cloud copy is refreshed when it changes).</summary>
        public static int Version { get; private set; }

        /// <summary>My online ID once signed in (so the match screen knows which seat shows my own photo).</summary>
        public static string MyOnlineId { get; set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            mine = null;
            loaded = false;
            others.Clear();
            Version = 0;
            MyOnlineId = "";
        }

        static string FilePath => Path.Combine(Application.persistentDataPath, "profile_photo.jpg");

        /// <summary>My photo, or null when I use an animal avatar.</summary>
        public static Sprite Mine
        {
            get
            {
                if (!loaded) LoadFromDisk();
                return mine;
            }
        }

        public static bool Has => Mine != null;

        /// <summary>The photo file as JPEG bytes (for the cloud), or null.</summary>
        public static byte[] MineBytes()
        {
            try { return File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null; }
            catch (Exception) { return null; }
        }

        /// <summary>Use this picture as my photo: cut to a centred square, shrunk to 128 x 128 and saved.</summary>
        public static bool SetMine(Texture source)
        {
            if (source == null) return false;
            try
            {
                var square = CropSquare(source, Size);
                File.WriteAllBytes(FilePath, square.EncodeToJPG(JpegQuality));
                UnityEngine.Object.Destroy(square);
                loaded = false;
                Version++;
                LoadFromDisk();
                GameSettings.NotifyChanged();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Could not save the profile photo: " + e.Message);
                return false;
            }
        }

        /// <summary>Set my photo from JPEG/PNG bytes (downloaded from Google/Facebook or the cloud).</summary>
        public static bool SetMine(byte[] image)
        {
            var tex = Decode(image);
            if (tex == null) return false;
            bool ok = SetMine(tex);
            UnityEngine.Object.Destroy(tex);
            return ok;
        }

        /// <summary>Go back to the animal avatar.</summary>
        public static void ClearMine()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning("[Ludo] Removing the photo: " + e.Message); }
            if (mine != null) DestroySprite(mine);
            mine = null;
            loaded = true;
            Version++;
            GameSettings.NotifyChanged();
        }

        /// <summary>The picture to show for this online player: my own photo for me, a downloaded photo for others, otherwise null.</summary>
        public static Sprite ForPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            if (playerId == MyOnlineId) return Mine;
            return others.TryGetValue(playerId, out var s) ? s : null;
        }

        public static bool IsCached(string playerId) => !string.IsNullOrEmpty(playerId) && (playerId == MyOnlineId ? Has : others.ContainsKey(playerId));

        /// <summary>Remember another player's photo (JPEG bytes from the cloud). Returns false when the bytes are not a picture.</summary>
        public static bool SetOther(string playerId, byte[] image)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            var tex = Decode(image);
            if (tex == null) return false;
            if (others.TryGetValue(playerId, out var old) && old != null) DestroySprite(old);
            others[playerId] = MakeSprite(tex);
            return true;
        }

        /// <summary>Someone changed or removed their photo: forget what we know so it is fetched again.</summary>
        public static void ForgetOther(string playerId)
        {
            if (others.TryGetValue(playerId, out var old) && old != null) DestroySprite(old);
            others.Remove(playerId);
        }

        // ---------- pictures ----------

        static void LoadFromDisk()
        {
            loaded = true;
            if (mine != null) { DestroySprite(mine); mine = null; }
            try
            {
                if (!File.Exists(FilePath)) return;
                var tex = Decode(File.ReadAllBytes(FilePath));
                if (tex != null) mine = MakeSprite(tex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludo] Could not read the profile photo: " + e.Message);
            }
        }

        static Texture2D Decode(byte[] image)
        {
            if (image == null || image.Length < 16) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(image, false)) { UnityEngine.Object.Destroy(tex); return null; }
            return tex;
        }

        static Sprite MakeSprite(Texture2D decoded)
        {
            // a decoded JPEG has no alpha channel (RGB24): copy it into an RGBA texture so the corners can be made transparent
            var tex = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, false);
            tex.SetPixels32(decoded.GetPixels32());
            UnityEngine.Object.Destroy(decoded);
            RoundCorners(tex);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "ProfilePhoto";
            return sprite;
        }

        static void DestroySprite(Sprite s)
        {
            if (s == null) return;
            var tex = s.texture;
            UnityEngine.Object.Destroy(s);
            if (tex != null) UnityEngine.Object.Destroy(tex);
        }

        /// <summary>A centred square of the picture, scaled to size x size (RGBA, readable).</summary>
        public static Texture2D CropSquare(Texture source, int size)
        {
            int side = Mathf.Min(source.width, source.height);
            float u = (source.width - side) * 0.5f / source.width;
            float v = (source.height - side) * 0.5f / source.height;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Graphics.Blit(source, rt, new Vector2((float)side / source.width, (float)side / source.height), new Vector2(u, v));
            RenderTexture.active = rt;
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        /// <summary>Make everything outside the inscribed circle transparent (with a one pixel soft edge) so the photo looks like the round avatars.</summary>
        public static void RoundCorners(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var pixels = tex.GetPixels32();
            float radius = Mathf.Min(w, h) * 0.5f;
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(radius - d);          // 1 inside, 0 more than a pixel outside
                    int i = y * w + x;
                    pixels[i].a = (byte)Mathf.RoundToInt(pixels[i].a * a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }
    }
}
