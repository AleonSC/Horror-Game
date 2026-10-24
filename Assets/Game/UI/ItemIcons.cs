using System.Collections.Generic;
using Game.Player;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Line drawings of the items for the hotbar and inventory, drawn in code the first time each is needed (white
    /// on clear, so they can be tinted): a flashlight, batteries, a torch, a lighter, the flare gun, glowsticks, a
    /// camera, the key fob, the map, the drone and the radio.
    /// </summary>
    public static class ItemIcons
    {
        const int Size = 64;
        static readonly Dictionary<ItemKind, Texture2D> made = new Dictionary<ItemKind, Texture2D>();

        public static Texture2D For(ItemKind kind)
        {
            if (kind == ItemKind.None) return null;
            if (made.TryGetValue(kind, out var tex) && tex != null) return tex;
            var c = new Canvas();
            Draw(c, kind);
            tex = c.ToTexture($"Icon {kind}");
            made[kind] = tex;
            return tex;
        }

        // (Drawn on a 64 x 64 grid, y down from the top.)
        static void Draw(Canvas c, ItemKind kind)
        {
            const float w = 3.2f;
            switch (kind)
            {
                case ItemKind.Flashlight:
                    c.Turn = -30f;
                    c.Box(10, 27, 38, 37, w);
                    c.Line(38, 27, 50, 20, w); c.Line(50, 20, 50, 44, w); c.Line(50, 44, 38, 37, w);
                    c.Fill(20, 24, 26, 27);
                    c.Turn = 0f;
                    c.Line(52, 10, 58, 6, 2f); c.Line(55, 16, 61, 15, 2f);
                    break;
                case ItemKind.Batteries:
                    c.Box(20, 14, 44, 56, w);
                    c.Fill(27, 8, 37, 14);
                    c.Line(35, 22, 27, 35, w); c.Line(27, 35, 37, 35, w); c.Line(37, 35, 29, 48, w);
                    break;
                case ItemKind.Torch:
                    c.Line(32, 60, 32, 30, 6f);
                    c.Line(27, 38, 37, 38, 2.5f); c.Line(27, 44, 37, 44, 2.5f);
                    c.Fill(25, 27, 39, 32);
                    c.Line(24, 22, 28, 11, w); c.Line(28, 11, 34, 3, w); c.Line(34, 3, 35, 10, w); c.Line(35, 10, 40, 14, w); c.Line(40, 14, 40, 22, w);
                    c.Line(32, 22, 31, 14, 2f); c.Line(31, 14, 35, 17, 2f);
                    break;
                case ItemKind.Lighter:
                    c.Box(20, 28, 44, 58, w);
                    c.Box(20, 20, 44, 28, w);
                    c.Line(26, 40, 38, 40, 2f); c.Line(26, 46, 38, 46, 2f);
                    c.Line(33, 16, 37, 4, 2.6f); c.Line(41, 16, 37, 4, 2.6f); c.Line(33, 16, 41, 16, 2.6f);
                    break;
                case ItemKind.FlareGun:
                    c.Box(10, 18, 54, 29, w);
                    c.Line(18, 29, 14, 52, 4.5f); c.Line(14, 52, 25, 52, w); c.Line(25, 52, 28, 29, w);
                    c.Ring(32, 34, 5, 2.5f);
                    c.Line(58, 23, 63, 23, 2f); c.Line(57, 15, 61, 11, 2f); c.Line(57, 31, 61, 35, 2f);
                    break;
                case ItemKind.Glowsticks:
                    c.Line(18, 50, 44, 14, 12f);
                    c.Erase(18, 50, 44, 14, 6f);
                    c.Line(28, 52, 52, 20, 9f);
                    c.Erase(28, 52, 52, 20, 3.5f);
                    c.Disc(12, 18, 2); c.Disc(54, 52, 2); c.Disc(8, 36, 1.5f);
                    break;
                case ItemKind.Camera:
                    c.Box(8, 22, 56, 52, w);
                    c.Box(20, 15, 32, 22, w);
                    c.Ring(32, 37, 10, w);
                    c.Ring(32, 37, 4.5f, 2.2f);
                    c.Fill(45, 26, 51, 30);
                    break;
                case ItemKind.KeyFob:
                    c.Ring(32, 13, 7, w);
                    c.Box(22, 24, 42, 58, w);
                    c.Disc(32, 34, 3.2f); c.Disc(32, 46, 3.2f);
                    break;
                case ItemKind.Map:
                    c.Line(8, 16, 23, 10, w); c.Line(23, 10, 41, 16, w); c.Line(41, 16, 56, 10, w);
                    c.Line(56, 10, 56, 48, w); c.Line(56, 48, 41, 54, w); c.Line(41, 54, 23, 48, w);
                    c.Line(23, 48, 8, 54, w); c.Line(8, 54, 8, 16, w);
                    c.Line(23, 10, 23, 48, 1.8f); c.Line(41, 16, 41, 54, 1.8f);
                    c.Disc(14, 42, 1.6f); c.Disc(19, 36, 1.6f); c.Disc(26, 33, 1.6f); c.Disc(33, 30, 1.6f); c.Disc(38, 26, 1.6f);
                    c.Line(43, 20, 51, 28, 2.6f); c.Line(51, 20, 43, 28, 2.6f);
                    break;
                case ItemKind.Drone:
                    c.Fill(25, 27, 39, 37);
                    foreach (var (x, y) in new[] { (13f, 17f), (51f, 17f), (13f, 47f), (51f, 47f) })
                    {
                        c.Line(32, 32, x, y, 2.6f);
                        c.Ring(x, y, 7.5f, 2.2f);
                    }
                    break;
                case ItemKind.Radio:
                    c.Box(8, 24, 56, 56, w);
                    c.Line(16, 24, 44, 6, 2.4f);
                    c.Disc(44, 6, 2.2f);
                    c.Ring(23, 40, 8, 2.4f);
                    c.Disc(23, 40, 2.2f);
                    c.Disc(44, 35, 3.5f);
                    c.Line(38, 46, 50, 46, 2.2f);
                    break;
                default:
                    c.Ring(32, 32, 16, w);
                    break;
            }
        }

        // A coverage canvas with anti-aliased strokes.
        sealed class Canvas
        {
            readonly float[] a = new float[Size * Size];
            public float Turn; // (degrees: strokes are turned round the middle by this)

            Vector2 T(float x, float y)
            {
                if (Turn == 0f) return new Vector2(x, y);
                float r = Turn * Mathf.Deg2Rad, cx = x - 32f, cy = y - 32f;
                return new Vector2(32f + cx * Mathf.Cos(r) - cy * Mathf.Sin(r), 32f + cx * Mathf.Sin(r) + cy * Mathf.Cos(r));
            }

            void Shape(Vector2 min, Vector2 max, System.Func<Vector2, float> coverage, bool erase = false)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(min.x) - 1), x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(max.x) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(min.y) - 1), y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(max.y) + 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float v = Mathf.Clamp01(coverage(new Vector2(x + 0.5f, y + 0.5f)));
                    int i = y * Size + x;
                    a[i] = erase ? Mathf.Min(a[i], 1f - v) : Mathf.Max(a[i], v);
                }
            }

            static float Segment(Vector2 p, Vector2 s, Vector2 e)
            {
                Vector2 d = e - s;
                float t = d.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - s, d) / d.sqrMagnitude) : 0f;
                return (p - (s + d * t)).magnitude;
            }

            void Stroke(float x0, float y0, float x1, float y1, float width, bool erase)
            {
                Vector2 s = T(x0, y0), e = T(x1, y1);
                float h = width * 0.5f;
                Shape(Vector2.Min(s, e) - Vector2.one * h, Vector2.Max(s, e) + Vector2.one * h, p => h - Segment(p, s, e) + 0.5f, erase);
            }

            public void Line(float x0, float y0, float x1, float y1, float width) => Stroke(x0, y0, x1, y1, width, false);
            public void Erase(float x0, float y0, float x1, float y1, float width) => Stroke(x0, y0, x1, y1, width, true);

            public void Box(float x0, float y0, float x1, float y1, float width)
            {
                Line(x0, y0, x1, y0, width); Line(x1, y0, x1, y1, width);
                Line(x1, y1, x0, y1, width); Line(x0, y1, x0, y0, width);
            }

            public void Fill(float x0, float y0, float x1, float y1)
            {
                // (As a fat line down the middle, so it turns with the rest.)
                float h = (y1 - y0) * 0.5f;
                Vector2 s = T(x0 + h, y0 + h), e = T(x1 - h, y0 + h);
                if (x1 - x0 < y1 - y0) { h = (x1 - x0) * 0.5f; s = T(x0 + h, y0 + h); e = T(x0 + h, y1 - h); }
                float r = h;
                Shape(Vector2.Min(s, e) - Vector2.one * r, Vector2.Max(s, e) + Vector2.one * r, p => r - Segment(p, s, e) + 0.5f);
            }

            public void Ring(float x, float y, float r, float width)
            {
                Vector2 c = T(x, y);
                float h = width * 0.5f;
                Shape(c - Vector2.one * (r + h), c + Vector2.one * (r + h), p => h - Mathf.Abs((p - c).magnitude - r) + 0.5f);
            }

            public void Disc(float x, float y, float r)
            {
                Vector2 c = T(x, y);
                Shape(c - Vector2.one * r, c + Vector2.one * r, p => r - (p - c).magnitude + 0.5f);
            }

            public Texture2D ToTexture(string name)
            {
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    px[(Size - 1 - y) * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a[y * Size + x] * 255f)); // (textures run bottom up)
                tex.SetPixels32(px);
                tex.Apply(false);
                return tex;
            }
        }
    }
}
