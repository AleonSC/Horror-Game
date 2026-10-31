using UnityEngine;

namespace Game.World.Build
{
    /// <summary>The water's textures, made in code: gentle ripples (a tiling normal map) and the puddles' shapes.</summary>
    public static class WaterTextures
    {
        /// <summary>Overlapping ripples, as a tiling normal map (linear), for still water.</summary>
        public static Texture2D Ripples()
        {
            const int n = 256;
            var height = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                height[y * n + x] = Tile(u * 6f, v * 6f, 6, 81) * 0.5f + Tile(u * 14f, v * 14f, 14, 82) * 0.3f + Tile(u * 32f, v * 32f, 32, 83) * 0.2f;
            }
            var px = new Color[n * n];
            const float strength = 2.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = height[y * n + Mod(x + 1, n)] - height[y * n + Mod(x - 1, n)];
                float dy = height[Mod(y + 1, n) * n + x] - height[Mod(y - 1, n) * n + x];
                var nv = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                px[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, 1f, 1f); // (as the forest's other normal maps)
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, true)
            {
                name = "Water ripples", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// A puddle's shape over its whole quad (white; the shape is in the alpha): an irregular pool with a soft,
        /// faint fringe of wet ground round it, gone well inside the quad's edge.
        /// </summary>
        public static Texture2D PuddleShape(int shape)
        {
            const int n = 128;
            int seed = 300 + shape * 17;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f; // 0 in the middle, 1 at the quad's edge
                float wobble = (Fbm(u, v, 3, 3, seed) - 0.5f) * 0.55f + (Fbm(u, v, 8, 2, seed + 5) - 0.5f) * 0.12f;
                float r = d + wobble;
                const float edge = 0.58f;
                float pool = 1f - Ramp(edge - 0.07f, edge + 0.02f, r);
                float wet = 1f - Ramp(edge, edge + 0.22f, r);
                float a = 0.9f * pool + 0.3f * Mathf.Max(0f, wet - pool);
                a *= 1f - Ramp(0.86f, 0.99f, d);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, false)
            {
                name = "Puddle " + shape, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static float Ramp(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        static int Mod(int a, int m) => ((a % m) + m) % m;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 144665;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        // Value noise on a lattice that wraps every `period` cells, so it tiles.
        static float Tile(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int ax = Mod(x0, period), bx = Mod(x0 + 1, period), ay = Mod(y0, period), by = Mod(y0 + 1, period);
            return Mathf.Lerp(Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx), Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx), fy);
        }

        static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += Tile(u * period, v * period, period, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }
    }

    /// <summary>Slowly drifts the ripples across the ponds (they share one material).</summary>
    public sealed class WaterDrift : MonoBehaviour
    {
        public Material material;
        public Vector2 drift = new Vector2(0.012f, 0.007f);
        Vector2 offset;

        void Update()
        {
            if (material == null) return;
            offset += drift * Time.deltaTime;
            material.SetTextureOffset("_BaseColorMap", offset); // (HDRP's main maps all follow this one's tiling)
        }
    }
}
