using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// Tree bark, made in code (it tiles both ways): U goes round the trunk, V up it. A pine's bark is thick plates,
    /// taller than wide, split by deep dark fissures and broken across into scales; dead wood is the same grain
    /// weathered grey and smoothed, its plates mostly flaked away. A colour map and a matching normal map, so the
    /// fissures catch your light and throw little shadows.
    /// </summary>
    public static class BarkTexture
    {
        const int Width = 256, Height = 512;
        const int CellsAcross = 9, CellsUp = 5;

        static int heightSeed = int.MinValue;
        static float[] heights, plateShade;

        /// <summary>How the material should tile it on the built-in trunks (twice round, once every 2.5 m up).</summary>
        public static readonly Vector2 Tiling = new Vector2(2f, 0.4f);

        // The bark's surface: plates (cells, stretched up the trunk) high, the fissures between them low, with
        // horizontal cracks across the plates and fine grain over everything. Kept for the last seed.
        static void Surface(int seed)
        {
            if (heights != null && heightSeed == seed) return;
            var rng = new System.Random(seed);
            var points = new Vector2[CellsAcross * CellsUp];
            var shade = new float[points.Length];
            var crack = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                shade[i] = (float)rng.NextDouble();
                crack[i] = (float)rng.NextDouble();
            }
            float cw = Width / (float)CellsAcross, ch = Height / (float)CellsUp;
            var h = new float[Width * Height];
            var plate = new float[Width * Height];
            float o = (float)rng.NextDouble() * 40f;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int gx = Mathf.FloorToInt(x / cw), gy = Mathf.FloorToInt(y / ch);
                float f1 = float.MaxValue, f2 = float.MaxValue;
                int nearest = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = gx + dx, cy = gy + dy;
                    int wx = (cx % CellsAcross + CellsAcross) % CellsAcross, wy = (cy % CellsUp + CellsUp) % CellsUp;
                    int k = wy * CellsAcross + wx;
                    float px = (cx + 0.15f + points[k].x * 0.7f) * cw, py = (cy + 0.15f + points[k].y * 0.7f) * ch;
                    // (wobbled a little so the fissures wander rather than run dead straight)
                    float ddx = x - px, ddy = (y - py) * 0.38f;
                    float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                    if (d < f1) { f2 = f1; f1 = d; nearest = k; }
                    else if (d < f2) f2 = d;
                }
                float u = x / (float)Width, v = y / (float)Height;
                float wander = GroundTexture.Tileable(u, v, 9, o) * 8f + (GroundTexture.Tileable(u, v, 24, o + 2f) - 0.5f) * 6f;
                float edge = f2 - f1 + wander - 5f;
                float top = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 7f));          // 0 in a fissure, 1 on a plate
                // A crack or two across each plate (where its own stripe falls).
                float across = Mathf.Abs(Mathf.Sin((v * CellsUp * 1.5f + crack[nearest] * 3f) * Mathf.PI)) + GroundTexture.Tileable(u, v, 30, o + 6f) - 0.3f;
                float cracked = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(across * 3f)) * 0.35f + 0.65f;
                float grain = GroundTexture.Tileable(u, v, 48, o + 5f) * 0.08f + Fibres(u, v, o + 9f) * 0.14f; // (and fibres running up it)
                h[y * Width + x] = top * (0.75f + shade[nearest] * 0.25f) * Mathf.Lerp(1f, cracked, top) + grain + (float)rng.NextDouble() * 0.04f;
                plate[y * Width + x] = top * shade[nearest];
            }
            heights = h;
            plateShade = plate;
            heightSeed = seed;
        }

        // Noise stretched up the trunk (fine across, long upward), tiling: the wood's fibres.
        static float Fibres(float u, float v, float seed)
        {
            const float across = 128f, up = 12f;
            float a = Mathf.PerlinNoise(u * across + seed, v * up + seed);
            float b = Mathf.PerlinNoise((u - 1f) * across + seed, v * up + seed);
            float c = Mathf.PerlinNoise(u * across + seed, (v - 1f) * up + seed);
            float d = Mathf.PerlinNoise((u - 1f) * across + seed, (v - 1f) * up + seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>The bark's colour. <paramref name="dead"/>: weathered grey dead wood rather than living pine.</summary>
        public static Texture2D Build(int seed, bool dead)
        {
            Surface(seed);
            var rng = new System.Random(seed * 7 + (dead ? 3 : 1));
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, true) { name = dead ? "Dead Wood" : "Bark", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color[Width * Height];
            Color deep = dead ? new Color(0.07f, 0.065f, 0.06f) : new Color(0.045f, 0.03f, 0.024f);
            Color plateLow = dead ? new Color(0.22f, 0.2f, 0.17f) : new Color(0.17f, 0.12f, 0.09f);
            Color plateHigh = dead ? new Color(0.36f, 0.33f, 0.29f) : new Color(0.3f, 0.22f, 0.16f);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int k = y * Width + x;
                float u = x / (float)Width, v = y / (float)Height;
                float hi = Mathf.Clamp01(heights[k]);
                Color plate = Color.Lerp(plateLow, plateHigh, Mathf.Clamp01(plateShade[k] * 0.7f + hi * 0.5f));
                Color c = Color.Lerp(deep, plate, Mathf.SmoothStep(0.15f, 0.6f, hi));
                // Patches of grey-green lichen on the living bark, and pale silvering on the dead.
                float patch = GroundTexture.Tileable(u, v, 5, 21f);
                if (dead) c = Color.Lerp(c, new Color(0.42f, 0.41f, 0.38f), Mathf.SmoothStep(0.6f, 0.8f, patch) * 0.5f * hi);
                else c = Color.Lerp(c, new Color(0.24f, 0.26f, 0.2f), Mathf.SmoothStep(0.68f, 0.85f, patch) * 0.6f * hi);
                px[k] = c;
            }
            for (int i = 0; i < 2500; i++) // flecks
            {
                int k = rng.Next(Width * Height);
                px[k] = Color.Lerp(px[k], dead ? new Color(0.45f, 0.43f, 0.4f) : new Color(0.36f, 0.27f, 0.2f), 0.25f + (float)rng.NextDouble() * 0.4f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>The bark's bumps for the lighting (a tangent-space normal map, linear), matching <see cref="Build"/>.</summary>
        public static Texture2D Normals(int seed, float strength = 6f)
        {
            Surface(seed);
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, true, true) { name = "Bark Normals", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color[Width * Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float hx = heights[y * Width + (x + 1) % Width] - heights[y * Width + (x + Width - 1) % Width];
                float hy = heights[((y + 1) % Height) * Width + x] - heights[((y + Height - 1) % Height) * Width + x];
                var n = new Vector3(-hx * strength, -hy * strength, 1f).normalized;
                px[y * Width + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, 1f, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
