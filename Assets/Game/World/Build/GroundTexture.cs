using UnityEngine;

namespace Game.World.Build
{
    /// <summary>The forest floor's texture, made in code (it tiles): dark earth with leaf litter; and the trails'.</summary>
    public static class GroundTexture
    {
        // Dark earth with leaf litter: specks and blotches of brown, ochre and near-black.
        public static Texture2D Build(int seed)
        {
            var rng = new System.Random(seed);
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Ground Litter", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[size * size];
            Color soil = new Color(0.11f, 0.085f, 0.065f), dark = new Color(0.05f, 0.04f, 0.035f), leaf = new Color(0.26f, 0.17f, 0.09f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Tileable(u, v, 4, 2f) * 0.6f + Tileable(u, v, 16, 9f) * 0.4f;
                px[y * size + x] = Color.Lerp(dark, soil, Mathf.SmoothStep(0.2f, 0.8f, n));
            }
            for (int i = 0; i < 900; i++) // leaves
            {
                int cx = rng.Next(size), cy = rng.Next(size);
                float r = 1.5f + (float)rng.NextDouble() * 3f;
                Color c = Color.Lerp(leaf, soil, (float)rng.NextDouble() * 0.7f);
                for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    if (dx * dx + dy * dy * 2f > r * r) continue;
                    int xx = (cx + dx + size) % size, yy = (cy + dy + size) % size;
                    px[yy * size + xx] = Color.Lerp(px[yy * size + xx], c, 0.8f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// The trails' packed earth (U across the trail, V along it, repeating): darker ruts and pale grit, solid
        /// down the middle and fading out raggedly to nothing at both sides, so a trail melts into the litter.
        /// </summary>
        public static Texture2D Trail(int seed)
        {
            var rng = new System.Random(seed * 7 + 3);
            const int w = 64, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Trail Earth", wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat };
            var px = new Color[w * h];
            Color earth = new Color(0.17f, 0.13f, 0.1f), rut = new Color(0.1f, 0.075f, 0.06f), grit = new Color(0.27f, 0.22f, 0.17f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = y / (float)h;
                float n = Tileable(u * 0.25f, v, 8, 4f) * 0.6f + Tileable(u * 0.25f, v, 32, 13f) * 0.4f;
                Color c = Color.Lerp(earth * 0.8f, earth * 1.15f, n);
                // Two faint ruts worn along it.
                float ruts = Mathf.Exp(-Mathf.Pow((Mathf.Abs(u - 0.5f) - 0.13f) / 0.05f, 2f));
                c = Color.Lerp(c, rut, ruts * 0.35f * Tileable(0.3f, v, 6, 21f));
                // Solid down the middle, out to a ragged edge that comes and goes along it.
                float t = Mathf.Abs(u - 0.5f) * 2f;
                float edge = Tileable(0.5f, v, 16, 7f) * 0.6f + Tileable(u * 0.5f, v, 48, 31f) * 0.4f;
                float a = 1f - Mathf.SmoothStep(0.3f + 0.25f * edge, 1f, t);
                a *= Mathf.Lerp(1f, 0.55f + 0.45f * Tileable(u, v, 40, 17f), Mathf.Clamp01(t * 1.5f - 0.3f)); // (patchy toward the sides)
                c.a = Mathf.Clamp01(a);
                px[y * w + x] = c;
            }
            for (int i = 0; i < 500; i++) // grit
            {
                int gx = rng.Next(w / 6, w - w / 6), gy = rng.Next(h);
                var c = px[gy * w + gx];
                var g = Color.Lerp(grit, earth, (float)rng.NextDouble() * 0.6f);
                g.a = c.a;
                px[gy * w + gx] = g;
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
