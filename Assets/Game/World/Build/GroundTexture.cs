using UnityEngine;

namespace Game.World.Build
{
    /// <summary>The forest floor's textures, made in code (they tile): fine dark soil and its bumps; and the trails'.</summary>
    public static class GroundTexture
    {
        const int SoilSize = 512;
        static int heightSeed = int.MinValue;
        static float[] soilHeight;

        // The soil's surface, tiling: a rolling base of earth, fine grain over everything, thousands of little
        // lumpy crumbs, the odd clod, and little pits. (Kept for the last seed, as the colour and the bumps both use it.)
        static float[] SoilHeight(int seed)
        {
            if (soilHeight != null && heightSeed == seed) return soilHeight;
            var rng = new System.Random(seed);
            const int size = SoilSize;
            var h = new float[size * size];
            float o = (float)rng.NextDouble() * 50f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                h[y * size + x] = Tileable(u, v, 6, o) * 0.25f + Tileable(u, v, 20, o + 3f) * 0.2f + Tileable(u, v, 64, o + 7f) * 0.18f
                                  + Tileable(u, v, 170, o + 13f) * 0.17f + (float)rng.NextDouble() * 0.2f;
            }
            // A crumb: a little lump of earth with a lumpy outline (a few lobes), added on (or, lift below 0, a pit).
            void Crumb(int cx, int cy, float r, float lift)
            {
                int ri = Mathf.CeilToInt(r * 1.3f);
                float phase = (float)rng.NextDouble() * 6.28f, jag = 0.25f + (float)rng.NextDouble() * 0.2f;
                int lobes = rng.Next(3, 6);
                for (int dy = -ri; dy <= ri; dy++)
                for (int dx = -ri; dx <= ri; dx++)
                {
                    float edge = r * (1f + jag * Mathf.Sin(Mathf.Atan2(dy, dx) * lobes + phase));
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / edge;
                    if (d >= 1f) continue;
                    int k = ((cy + dy + size) % size) * size + (cx + dx + size) % size;
                    h[k] += lift * Mathf.Pow(1f - d * d, 0.7f);
                }
            }
            for (int i = 0; i < 9000; i++) Crumb(rng.Next(size), rng.Next(size), 0.8f + (float)rng.NextDouble() * 1.8f, 0.12f + (float)rng.NextDouble() * 0.2f); // crumbs
            for (int i = 0; i < 120; i++) Crumb(rng.Next(size), rng.Next(size), 3f + (float)rng.NextDouble() * 4f, 0.25f + (float)rng.NextDouble() * 0.2f);    // clods
            for (int i = 0; i < 1500; i++) Crumb(rng.Next(size), rng.Next(size), 0.8f + (float)rng.NextDouble() * 1.5f, -0.18f);                              // pits
            heightSeed = seed;
            soilHeight = h;
            return h;
        }

        /// <summary>
        /// The forest floor's colour, made in code (it tiles, about 3 m across): dark, fine soil, crumbs and clods a
        /// shade lighter on top, darker in the hollows, and a scatter of pale grit.
        /// </summary>
        public static Texture2D Build(int seed)
        {
            var h = SoilHeight(seed);
            var rng = new System.Random(seed * 3 + 1);
            const int size = SoilSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Ground Soil", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color[size * size];
            Color low = new Color(0.04f, 0.03f, 0.022f), high = new Color(0.13f, 0.095f, 0.068f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float tint = Tileable(u, v, 4, 11f);
                Color c = Color.Lerp(low, high, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, h[y * size + x])));
                px[y * size + x] = Color.Lerp(c, new Color(c.r * 0.85f, c.g * 0.9f, c.b * 0.95f), tint); // (patches a touch greyer)
            }
            for (int i = 0; i < 9000; i++) // grit: single pale grains
            {
                int k = rng.Next(size * size);
                px[k] = Color.Lerp(px[k], new Color(0.26f, 0.21f, 0.15f), 0.3f + (float)rng.NextDouble() * 0.5f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// The forest floor's bumps for the lighting (a tangent-space normal map, linear, matching <see cref="Build"/>):
        /// the crumbs, clods and pits catch the flashlight and throw tiny shadows.
        /// </summary>
        public static Texture2D Normals(int seed, float strength = 3.5f)
        {
            var h = SoilHeight(seed);
            const int size = SoilSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "Ground Soil Normals", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float hx = h[y * size + (x + 1) % size] - h[y * size + (x + size - 1) % size];
                float hy = h[((y + 1) % size) * size + x] - h[((y + size - 1) % size) * size + x];
                var n = new Vector3(-hx * strength, -hy * strength, 1f).normalized;
                // (HDRP reads X from red times alpha, Y from green.)
                px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, 1f, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// The trails' sandy, packed earth (U from the middle line, 0.5, out to the edge of the fade, 0; V over the
        /// ground, repeating): paler than the forest floor, with darker ruts, grit and small stones; solid out to
        /// <paramref name="solidShare"/> of the way from the middle, then fading to nothing.
        /// </summary>
        public static Texture2D Trail(int seed, float solidShare)
        {
            var rng = new System.Random(seed * 7 + 3);
            const int w = 64, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Trail Earth", wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat };
            var px = new Color[w * h];
            Color earth = new Color(0.27f, 0.21f, 0.15f), rut = new Color(0.16f, 0.12f, 0.085f), grit = new Color(0.4f, 0.34f, 0.26f), stone = new Color(0.12f, 0.11f, 0.1f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = y / (float)h;
                float n = Tileable(u * 0.25f, v, 8, 4f) * 0.6f + Tileable(u * 0.25f, v, 32, 13f) * 0.4f;
                Color c = Color.Lerp(earth * 0.82f, earth * 1.12f, n);
                // Two faint ruts worn along it, and the middle a touch paler where it's walked most.
                float t = Mathf.Abs(u - 0.5f) * 2f;
                float ruts = Mathf.Exp(-Mathf.Pow((t - 0.26f) / 0.09f, 2f));
                c = Color.Lerp(c, rut, ruts * 0.3f);
                c = Color.Lerp(c, earth * 1.15f, (1f - Mathf.SmoothStep(0f, 0.25f, t)) * 0.25f);
                // Toward the edge it goes darker (the litter creeping in) as it fades out.
                c = Color.Lerp(c, new Color(0.13f, 0.1f, 0.075f), Mathf.SmoothStep(solidShare * 0.8f, 1f, t) * 0.6f);
                float a = 1f - Mathf.SmoothStep(solidShare, 1f, t);
                a *= Mathf.Lerp(1f, 0.6f + 0.4f * Tileable(u, v, 40, 17f), Mathf.SmoothStep(solidShare, 1f, t)); // (patchy as it fades)
                c.a = Mathf.Clamp01(a);
                px[y * w + x] = c;
            }
            for (int i = 0; i < 700; i++) // grit and small stones
            {
                int gx = rng.Next(w), gy = rng.Next(h);
                bool isStone = rng.NextDouble() < 0.3;
                var c = px[gy * w + gx];
                var g = Color.Lerp(isStone ? stone : grit, c, (float)rng.NextDouble() * 0.5f);
                g.a = c.a;
                px[gy * w + gx] = g;
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// The fine grain laid over the trails in world space (HDRP's detail map, so it's linear): little pebbles and
        /// grit raised out of the earth, catching the light. R: lighter and darker (0.5 none); A and G: the surface's
        /// tilt; B: shine (0.5 none).
        /// </summary>
        public static Texture2D TrailDetail(int seed)
        {
            var rng = new System.Random(seed * 11 + 9);
            const int size = 128;
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                height[y * size + x] = Tileable(x / (float)size, y / (float)size, 32, 3f) * 0.25f;
            for (int i = 0; i < 260; i++) // pebbles
            {
                int cx = rng.Next(size), cy = rng.Next(size);
                float r = 1.5f + (float)rng.NextDouble() * 3.5f, lift = 0.4f + (float)rng.NextDouble() * 0.6f;
                int ri = Mathf.CeilToInt(r);
                for (int dy = -ri; dy <= ri; dy++)
                for (int dx = -ri; dx <= ri; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (d >= 1f) continue;
                    int k = ((cy + dy + size) % size) * size + (cx + dx + size) % size;
                    height[k] = Mathf.Max(height[k], lift * Mathf.Sqrt(1f - d * d));
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "Trail Grain", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[size * size];
            const float strength = 2.2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float hx = height[y * size + (x + 1) % size] - height[y * size + (x + size - 1) % size];
                float hy = height[((y + 1) % size) * size + x] - height[((y + size - 1) % size) * size + x];
                var normal = new Vector3(-hx * strength, -hy * strength, 1f).normalized;
                float hgt = height[y * size + x];
                px[y * size + x] = new Color(0.5f + (hgt - 0.3f) * 0.4f, normal.y * 0.5f + 0.5f, 0.5f + hgt * 0.2f, normal.x * 0.5f + 0.5f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static float Tileable(float u, float v, int period, float seed)
        {
            // Blend four offset samples so the noise wraps at the texture edge.
            float s = period;
            float a = Mathf.PerlinNoise(u * s + seed, v * s + seed);
            float b = Mathf.PerlinNoise((u - 1f) * s + seed, v * s + seed);
            float c = Mathf.PerlinNoise(u * s + seed, (v - 1f) * s + seed);
            float d = Mathf.PerlinNoise((u - 1f) * s + seed, (v - 1f) * s + seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
