using UnityEngine;

namespace Game.World.Build
{
    /// <summary>The forest floor's texture, made in code (it tiles): dark earth with leaf litter.</summary>
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
