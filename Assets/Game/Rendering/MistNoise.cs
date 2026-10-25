using Game.Core;
using UnityEngine;

namespace Game.Rendering
{
    /// <summary>
    /// The ground mist's density, as a 3D texture: soft patches that tile seamlessly across (x, z), and fade
    /// out with height (y) so the mist hugs the ground. The density maths is plain C# so it can be tested.
    /// </summary>
    public static class MistNoise
    {
        public const int Size = 64;      // across (x and z)
        public const int Height = 16;    // up (y)
        const int Cells = 4;             // noise cells across the tile (more: smaller patches)

        /// <summary>Density 0-1 at a point in the tile (u, w across, 0-1; v up, 0-1).</summary>
        public static float Density(float u, float v, float w, float patchiness, int seed)
        {
            float n = 0.65f * TileNoise(u, w, v, Cells, seed) + 0.35f * TileNoise(u, w, v * 2f, Cells * 2, seed + 17);
            float patches = Mathf.Lerp(1f, MathUtil.SmoothThreshold(0.25f, 0.8f, n), Mathf.Clamp01(patchiness));
            // Thickest at the ground, thinning with height and gone at the top of the box.
            float height = 1f - Mathf.SmoothStep(0f, 1f, v);
            return Mathf.Clamp01(patches * height * height);
        }

        /// <summary>
        /// The ground mist's density at a point in the world (0-1): soft patches about <paramref name="patchSize"/>
        /// metres across (scrolled along by <paramref name="offset"/>), thinning with <paramref name="up"/> (the
        /// height above the ground as a share of the mist's height, 0-1) and gone at the top.
        /// </summary>
        public static float Ground(float x, float z, float up, float patchiness, float patchSize, Vector2 offset, int seed)
        {
            if (up >= 1f) return 0f;
            float px = (x + offset.x) / Mathf.Max(1f, patchSize), pz = (z + offset.y) / Mathf.Max(1f, patchSize);
            float py = Mathf.Max(0f, up) * 2f;
            float n = 0.65f * Value(px, py, pz, seed) + 0.35f * Value(px * 2f, py * 2f, pz * 2f, seed + 17);
            float patches = Mathf.Lerp(1f, MathUtil.SmoothThreshold(0.25f, 0.8f, n), Mathf.Clamp01(patchiness));
            float height = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(up));
            return Mathf.Clamp01(patches * height * height);
        }

        // Smooth value noise in open space (not tiling).
        static float Value(float x, float y, float z, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
            float fx = Smooth(x - x0), fy = Smooth(y - y0), fz = Smooth(z - z0);
            float a = Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, z0, seed), Hash(x0 + 1, y0, z0, seed), fx),
                                 Mathf.Lerp(Hash(x0, y0, z0 + 1, seed), Hash(x0 + 1, y0, z0 + 1, seed), fx), fz);
            float b = Mathf.Lerp(Mathf.Lerp(Hash(x0, y0 + 1, z0, seed), Hash(x0 + 1, y0 + 1, z0, seed), fx),
                                 Mathf.Lerp(Hash(x0, y0 + 1, z0 + 1, seed), Hash(x0 + 1, y0 + 1, z0 + 1, seed), fx), fz);
            return Mathf.Lerp(a, b, fy);
        }

        public static Texture3D Build(float patchiness, int seed = 1)
        {
            var tex = new Texture3D(Size, Height, Size, TextureFormat.Alpha8, false)
            {
                name = "Ground Mist Density",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                wrapModeW = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[Size * Height * Size];
            for (int z = 0; z < Size; z++)
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Density((x + 0.5f) / Size, (y + 0.5f) / Height, (z + 0.5f) / Size, patchiness, seed);
                pixels[x + y * Size + z * Size * Height] = new Color32(0, 0, 0, (byte)(d * 255f + 0.5f));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        // Smooth value noise that wraps every 'cells' cells across (u, w), so the tile repeats without seams.
        static float TileNoise(float u, float w, float v, int cells, int seed)
        {
            float x = u * cells, z = w * cells, y = v * 2f;
            int x0 = Mathf.FloorToInt(x), z0 = Mathf.FloorToInt(z), y0 = Mathf.FloorToInt(y);
            float fx = Smooth(x - x0), fz = Smooth(z - z0), fy = Smooth(y - y0);
            float Corner(int cx, int cy, int cz) => Hash(Wrap(cx, cells), cy, Wrap(cz, cells), seed);
            float a = Mathf.Lerp(Mathf.Lerp(Corner(x0, y0, z0), Corner(x0 + 1, y0, z0), fx),
                                 Mathf.Lerp(Corner(x0, y0, z0 + 1), Corner(x0 + 1, y0, z0 + 1), fx), fz);
            float b = Mathf.Lerp(Mathf.Lerp(Corner(x0, y0 + 1, z0), Corner(x0 + 1, y0 + 1, z0), fx),
                                 Mathf.Lerp(Corner(x0, y0 + 1, z0 + 1), Corner(x0 + 1, y0 + 1, z0 + 1), fx), fz);
            return Mathf.Lerp(a, b, fy);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
        static int Wrap(int i, int n) => ((i % n) + n) % n;

        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647 + seed * 144665);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
