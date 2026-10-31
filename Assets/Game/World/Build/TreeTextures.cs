using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The birch's bark and the willow's strands, made in code. Birch bark: pale and papery, scored across with dark
    /// lenticels, smudged with dark patches, and here and there a dark, almond-shaped knot where a branch fell
    /// away, with a dark brow over it: the "eyes" birches are known for. Willow strand: a thread hung thick with
    /// narrow leaves (the shape in the alpha).
    /// </summary>
    public static class TreeTextures
    {
        /// <summary>How far along the trunk the bark texture runs before repeating (metres).</summary>
        public const float BirchRepeat = 2.5f;

        /// <summary>Birch bark: u once round the trunk, v <see cref="BirchRepeat"/> metres up it. Tiles both ways.</summary>
        public static Texture2D BirchBark(int seed)
        {
            const int w = 128, h = 512;
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            var shade = new float[w * h];
            float ox = R() * 100f, oy = R() * 100f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                // Soft blotches of grey, wrapped so both edges meet.
                float n = Tiled(u, v, 4f, 6f, ox, oy) * 0.6f + Tiled(u, v, 12f, 30f, ox + 7f, oy + 3f) * 0.4f;
                shade[y * w + x] = 0.86f - 0.16f * n;
            }
            void Darken(int x, int y, float amount)
            {
                x = ((x % w) + w) % w; y = ((y % h) + h) % h;
                shade[y * w + x] = Mathf.Min(shade[y * w + x], Mathf.Lerp(shade[y * w + x], 0.08f, amount));
            }
            // Big dark patches, mostly where branches were.
            for (int b = 0; b < 7; b++)
            {
                int cx = (int)(R() * w), cy = (int)(R() * h);
                float rx = 6f + R() * 18f, ry = 3f + R() * 10f;
                for (int dy = -(int)ry - 2; dy <= ry + 2; dy++)
                for (int dx = -(int)rx - 2; dx <= rx + 2; dx++)
                {
                    float e = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
                    float edge = Mathf.PerlinNoise((cx + dx) * 0.3f + ox, (cy + dy) * 0.3f) * 0.6f;
                    if (e < 1f - edge) Darken(cx + dx, cy + dy, 0.75f);
                }
            }
            // Lenticels: short dark dashes running round the trunk.
            for (int l = 0; l < 220; l++)
            {
                int cx = (int)(R() * w), cy = (int)(R() * h);
                int len = 4 + (int)(R() * R() * 34f);
                int thick = R() < 0.7f ? 1 : 2;
                float dark = 0.35f + R() * 0.45f;
                for (int i = 0; i < len; i++)
                for (int t = 0; t < thick; t++)
                    Darken(cx + i, cy + t, dark * (1f - Mathf.Abs(i - len * 0.5f) / len));
            }
            // The eyes: dark almond knots with a curved brow over each.
            int eyes = 3;
            for (int e = 0; e < eyes; e++)
            {
                int cx = (int)((e + 0.2f + R() * 0.6f) / eyes * w), cy = (int)((e + 0.2f + R() * 0.6f) / eyes * h);
                float ew = 8f + R() * 5f, eh = 3.5f + R() * 2f;
                for (int dy = -12; dy <= 12; dy++)
                for (int dx = -(int)ew - 6; dx <= ew + 6; dx++)
                {
                    float fx = dx / ew;
                    if (Mathf.Abs(fx) <= 1f)
                    {
                        float half = eh * Mathf.Pow(1f - fx * fx, 0.8f);
                        if (Mathf.Abs(dy) <= half) Darken(cx + dx, cy + dy, 0.97f);
                        else if (Mathf.Abs(dy) <= half + 1.2f) Darken(cx + dx, cy + dy, 0.6f);
                    }
                    // The brow: an arch above (up the trunk is +v), spreading out past the eye's corners.
                    float bx = dx / (ew + 5f);
                    if (Mathf.Abs(bx) <= 1f)
                    {
                        float browY = eh + 4f + 4f * (1f - bx * bx);
                        if (Mathf.Abs(dy - browY) < 1.2f + 0.8f * (1f - Mathf.Abs(bx))) Darken(cx + dx, cy + dy, 0.85f);
                    }
                }
            }
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                float c = shade[i] + (R() - 0.5f) * 0.03f;
                px[i] = new Color(c, c * 0.97f, c * 0.9f, 1f);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = "Birch bark", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// A metre and a half of willow strand (u across it, v along it, tiling along): a thin thread with narrow
        /// leaves hanging off both sides, close together. White, with a little variation; tint it with the material.
        /// </summary>
        public static Texture2D WillowStrand(int seed)
        {
            const int w = 32, h = 256;
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.8f, 0.85f, 0.7f, 0f);
            void Paint(int x, int y, float c)
            {
                if (x < 0 || x >= w) return;
                y = ((y % h) + h) % h;
                px[y * w + x] = new Color(c, c * 1.05f, c * 0.8f, 1f);
            }
            for (int y = 0; y < h; y++) Paint(w / 2, y, 0.55f); // the thread
            // Leaves every few pixels down each side, angled down and out, narrow and pointed.
            for (int y0 = 0; y0 < h; y0 += 5)
            for (int sideSign = -1; sideSign <= 1; sideSign += 2)
            {
                float len = 11f + R() * 5f, wid = 1.6f + R() * 1.2f;
                float ang = (0.35f + R() * 0.35f) * Mathf.PI * 0.5f; // from straight down, outward
                Vector2 dir = new Vector2(Mathf.Sin(ang) * sideSign, Mathf.Cos(ang));
                Vector2 perp = new Vector2(-dir.y, dir.x);
                float c = 0.7f + R() * 0.3f;
                int start = y0 + (sideSign > 0 ? 2 : 0);
                for (float t = 0f; t <= len; t += 0.5f)
                {
                    float half = wid * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len) * 0.95f + 0.05f);
                    for (float q = -half; q <= half; q += 0.5f)
                    {
                        Vector2 p = new Vector2(w / 2f, start) + dir * t + perp * q;
                        Paint(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), c * (0.9f + 0.1f * (1f - Mathf.Abs(q) / Mathf.Max(half, 0.01f))));
                    }
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = "Willow strand", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave,
            };
            tex.wrapModeU = TextureWrapMode.Clamp;
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // Value noise in 0..1 that wraps at both edges of the unit square (cells across, cells along).
        static float Tiled(float u, float v, float cellsU, float cellsV, float ox, float oy)
        {
            float a = Mathf.PerlinNoise(u * cellsU + ox, v * cellsV + oy);
            float b = Mathf.PerlinNoise((u - 1f) * cellsU + ox, v * cellsV + oy);
            float c = Mathf.PerlinNoise(u * cellsU + ox, (v - 1f) * cellsV + oy);
            float d = Mathf.PerlinNoise((u - 1f) * cellsU + ox, (v - 1f) * cellsV + oy);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
