using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Smooth value noise that tiles: it repeats every <c>cells</c> cells, so laid over the map with whole cells
    /// it wraps seamlessly at the edges. Pure maths, safe on any thread.
    /// </summary>
    public static class PeriodicNoise
    {
        /// <summary>Noise 0-1 at (x, z) on a map <paramref name="size"/> across, with <paramref name="cells"/> cells across it.</summary>
        public static float Sample(float x, float z, float size, int cells, int seed)
        {
            cells = Mathf.Max(1, cells);
            float u = x / size * cells, v = z / size * cells;
            int x0 = Mathf.FloorToInt(u), z0 = Mathf.FloorToInt(v);
            float fx = Smooth(u - x0), fz = Smooth(v - z0);
            float a = Hash(Mod(x0, cells), Mod(z0, cells), seed);
            float b = Hash(Mod(x0 + 1, cells), Mod(z0, cells), seed);
            float c = Hash(Mod(x0, cells), Mod(z0 + 1, cells), seed);
            float d = Hash(Mod(x0 + 1, cells), Mod(z0 + 1, cells), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }

        /// <summary>A few octaves of it (each twice as fine, half as strong), 0-1.</summary>
        public static float Fractal(float x, float z, float size, int cells, int seed, int octaves = 3)
        {
            float sum = 0f, weight = 0f, amp = 1f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Sample(x, z, size, cells << o, seed + o * 131) * amp;
                weight += amp;
                amp *= 0.5f;
            }
            return sum / weight;
        }

        /// <summary>Noise 0-1 round a circle (for ragged outlines): <paramref name="angle"/> in radians.</summary>
        public static float Around(float angle, int lobes, int seed)
        {
            float u = angle / (Mathf.PI * 2f) * lobes;
            int i0 = Mathf.FloorToInt(u);
            float f = Smooth(u - i0);
            return Mathf.Lerp(Hash(Mod(i0, lobes), 7, seed), Hash(Mod(i0 + 1, lobes), 7, seed), f);
        }

        /// <summary>Smooth 1D noise 0-1 along a line, changing over about <paramref name="cellLength"/>.</summary>
        public static float Line(float x, float cellLength, int seed)
        {
            float u = x / Mathf.Max(0.001f, cellLength);
            int i0 = Mathf.FloorToInt(u);
            return Mathf.Lerp(Hash(i0, 3, seed), Hash(i0 + 1, 3, seed), Smooth(u - i0));
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
        static int Mod(int i, int n) => ((i % n) + n) % n;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
