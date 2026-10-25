using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// The beam's shape, like a real flashlight's: a hot centre, a softer spill round it fading to the edge,
    /// faint rings from the reflector, a darker ring round the hot spot, and a cheap lens's unevenness.
    /// </summary>
    public static class FlashlightCookie
    {
        public const int Size = 256;

        /// <summary>Brightness 0-1 at distance r from the centre (0 centre, 1 edge). feather: how soft the edge is.</summary>
        public static float Value(float dx, float dy, float feather)
        {
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float inner = Mathf.Lerp(0.85f, 0.15f, Mathf.Clamp01(feather)); // where the edge fade starts
            float spill = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 0.98f, r));
            float hot = Mathf.Exp(-Mathf.Pow(r / 0.28f, 2f));
            float v = spill * 0.42f + hot * 0.58f;
            v *= 1f + 0.07f * Mathf.Sin(r * 46f) * MathUtil.SmoothThreshold(0.1f, 0.4f, r);   // reflector rings
            v *= 1f - 0.12f * Mathf.Exp(-Mathf.Pow((r - 0.2f) / 0.025f, 2f));       // darker ring round the hot spot
            v *= 0.94f + 0.06f * Mathf.PerlinNoise(dx * 6f + 3.1f, dy * 6f + 7.7f);  // the lens
            return r >= 1f ? 0f : Mathf.Clamp01(v);
        }

        public static Texture2D Build(float feather)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = "Flashlight Cookie",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (x + 0.5f) / Size * 2f - 1f, dy = (y + 0.5f) / Size * 2f - 1f;
                byte b = (byte)Mathf.RoundToInt(Value(dx, dy, feather) * 255f);
                pixels[y * Size + x] = new Color32(b, b, b, b);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
