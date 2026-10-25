using System;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The pixels of a made-in-code surface, ready to become textures: colour, bumps (a tangent-space normal map,
    /// HDRP's layout) and HDRP's mask map (G: how shut-in each spot is, so crevices darken; A: shine). Kept apart
    /// from Unity's textures so the same code can be previewed outside the editor.
    /// </summary>
    public sealed class SurfacePixels
    {
        public int Width, Height;
        public Color[] Albedo, Normals, Mask;
    }

    /// <summary>
    /// Builds surfaces from a height field painted by stamping pieces onto it (needles, leaves, twigs, stones,
    /// flakes of bark), each lying on what's already there: the highest thing at a spot is what you see. Colour,
    /// bumps, crevice shading and shine all come from the same stamps, so they agree. Everything tiles.
    /// </summary>
    public sealed class SurfaceCanvas
    {
        public readonly int W, H;
        public readonly float[] Height;
        public readonly Color[] Colour;
        public readonly float[] Shine;

        public SurfaceCanvas(int width, int height)
        {
            W = width;
            H = height;
            Height = new float[W * H];
            Colour = new Color[W * H];
            Shine = new float[W * H];
        }

        public int Index(int x, int y) => ((y % H + H) % H) * W + (x % W + W) % W;

        public float HeightAt(float x, float y) => Height[Index(Mathf.RoundToInt(x), Mathf.RoundToInt(y))];

        // Lay one spot of something on top, if it stands above what's there.
        void Put(int x, int y, float h, Color c, float shine)
        {
            int k = Index(x, y);
            if (h <= Height[k]) return;
            Height[k] = h;
            Colour[k] = c;
            Shine[k] = shine;
        }

        /// <summary>
        /// A thin rounded strand (a pine needle, a twig, a grass blade lying down) from a to b, this wide, lying at
        /// <paramref name="rest"/> and standing <paramref name="thick"/> above it at its middle; tapering at the ends.
        /// The colour runs from <paramref name="from"/> at a to <paramref name="to"/> at b.
        /// </summary>
        public void Strand(Vector2 a, Vector2 b, float width, float rest, float thick, Color from, Color to, float shine, float taper = 0.3f)
        {
            Vector2 d = b - a;
            float len2 = Mathf.Max(1e-4f, d.sqrMagnitude);
            float half = width * 0.5f;
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half + 1);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / len2);
                float end = Mathf.Min(t, 1f - t) * 2f;                         // 0 at the ends, 1 in the middle
                float w = half * Mathf.Lerp(1f - taper, 1f, Mathf.Clamp01(end * 3f));
                float dist = (p - (a + d * t)).magnitude;
                if (dist >= w) continue;
                float across = dist / Mathf.Max(0.3f, w);
                float profile = Mathf.Sqrt(Mathf.Max(0f, 1f - across * across));
                // (lit from one edge, shaded on the other: the strand's own roundness in its colour)
                Color c = Color.Lerp(from, to, t) * Mathf.Lerp(0.8f, 1.05f, profile);
                Put(x, y, rest + thick * profile, c, shine);
            }
        }

        /// <summary>
        /// A fallen leaf: an oval (lobed if <paramref name="lobes"/> &gt; 0) pointing along <paramref name="angle"/>,
        /// its edges curled up as it dries, a paler midrib, and holes where it's rotted (decay 0-1).
        /// </summary>
        public void Leaf(Vector2 at, float length, float angle, float rest, Color colour, float lobes, float decay, int seed)
        {
            float half = length * 0.5f, wide = length * 0.32f;
            Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), side = new Vector2(-along.y, along.x);
            int r = Mathf.CeilToInt(half + 2);
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                var p = new Vector2(x, y);
                float u = Vector2.Dot(p, along) / half;                      // -1 stalk end .. 1 tip
                float v = Vector2.Dot(p, side);
                if (u < -1f || u > 1f) continue;
                float edge = wide * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * (1f - 0.35f * Mathf.Max(0f, u));
                if (lobes > 0f) edge *= 0.78f + 0.22f * Mathf.Abs(Mathf.Cos(u * lobes * Mathf.PI * 0.5f));
                float s = Mathf.Abs(v) / Mathf.Max(0.5f, edge);
                if (s >= 1f) continue;
                if (decay > 0f && Noise.Fbm((at.x + x) * 0.15f, (at.y + y) * 0.15f, 3, seed) > 1f - decay * 0.5f) continue;
                float curl = s * s * 0.6f;
                float rib = Mathf.Exp(-v * v / 1.2f) * (1f - Mathf.Abs(u)) * 0.12f;
                float veins = Mathf.Abs(Mathf.Sin((u * 6f - Mathf.Abs(v) / Mathf.Max(1f, wide) * 3f) * Mathf.PI)) < 0.12f ? 0.05f : 0f;
                Color c = colour * (0.85f + 0.3f * s - rib * 0.5f) + new Color(rib, rib * 0.8f, rib * 0.5f) * 0.6f;
                c *= 1f - 0.25f * Noise.Fbm((at.x + x) * 0.3f, (at.y + y) * 0.3f, 2, seed + 7); // (blotches)
                Put(Mathf.RoundToInt(at.x) + x, Mathf.RoundToInt(at.y) + y, rest + 0.25f + curl + rib + veins, c, 0.3f);
            }
        }

        /// <summary>A stone or pebble: a lumpy dome, speckled.</summary>
        public void Stone(Vector2 at, float radius, float rest, float tall, Color colour, int seed)
        {
            int r = Mathf.CeilToInt(radius * 1.3f + 1);
            float phase = seed * 1.7f;
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                float a = Mathf.Atan2(y, x);
                float edge = radius * (1f + 0.18f * Mathf.Sin(a * 3f + phase) + 0.08f * Mathf.Sin(a * 7f + phase * 2f));
                float d = Mathf.Sqrt(x * x + y * y) / edge;
                if (d >= 1f) continue;
                float dome = Mathf.Pow(1f - d * d, 0.6f);
                float speck = Noise.Hash(Mathf.RoundToInt(at.x) + x, Mathf.RoundToInt(at.y) + y, seed);
                Color c = colour * (0.75f + 0.35f * dome) * (0.85f + 0.3f * speck);
                Put(Mathf.RoundToInt(at.x) + x, Mathf.RoundToInt(at.y) + y, rest + tall * dome, c, 0.35f);
            }
        }

        /// <summary>
        /// Finish: the crevice shading (how far below its surroundings each spot sits), the bumps and the shine.
        /// <paramref name="bumps"/> scales the normal map; <paramref name="shade"/> is how much of the crevice
        /// darkening goes into the colour itself (the flashlight's direct light gets none of HDRP's own).
        /// </summary>
        public SurfacePixels Finish(float bumps, float shade, int blur = 6)
        {
            var around = BoxBlur(Height, W, H, blur);
            around = BoxBlur(around, W, H, blur);
            var result = new SurfacePixels { Width = W, Height = H, Albedo = new Color[W * H], Normals = new Color[W * H], Mask = new Color[W * H] };
            System.Threading.Tasks.Parallel.For(0, H, y =>
            {
                for (int x = 0; x < W; x++)
                {
                    int k = y * W + x;
                    float hollow = Mathf.Max(0f, around[k] - Height[k]);
                    float ao = Mathf.Clamp(1f - hollow * 1.6f, 0.25f, 1f);
                    var c = Colour[k] * Mathf.Lerp(1f, ao, shade);
                    c.a = 1f;
                    result.Albedo[k] = c;
                    float hx = Height[Index(x + 1, y)] - Height[Index(x - 1, y)];
                    float hy = Height[Index(x, y + 1)] - Height[Index(x, y - 1)];
                    var n = new Vector3(-hx * bumps, -hy * bumps, 1f).normalized;
                    result.Normals[k] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, 1f, 1f);
                    result.Mask[k] = new Color(0f, ao, 1f, Shine[k]);
                }
            });
            return result;
        }

        // A wrapping box blur (separable), radius r.
        static float[] BoxBlur(float[] src, int w, int h, int r)
        {
            var tmp = new float[src.Length];
            var dst = new float[src.Length];
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < h; y++)
            {
                float sum = 0f;
                for (int i = -r; i <= r; i++) sum += src[y * w + (i % w + w) % w];
                for (int x = 0; x < w; x++)
                {
                    tmp[y * w + x] = sum * inv;
                    sum += src[y * w + (x + r + 1) % w] - src[y * w + ((x - r) % w + w) % w];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int i = -r; i <= r; i++) sum += tmp[((i % h + h) % h) * w + x];
                for (int y = 0; y < h; y++)
                {
                    dst[y * w + x] = sum * inv;
                    sum += tmp[((y + r + 1) % h) * w + x] - tmp[(((y - r) % h + h) % h) * w + x];
                }
            }
            return dst;
        }
    }

    /// <summary>
    /// Gradient noise that repeats exactly every <c>period</c> cells (so textures made with it tile), and layered
    /// (fractal) noise built from it. Pure code (no Unity calls), quick enough to fill big textures.
    /// </summary>
    public static class Noise
    {
        /// <summary>0 below <paramref name="from"/>, 1 above <paramref name="to"/>, easing between (a smooth threshold).</summary>
        public static float Step(float from, float to, float x) => Game.Core.MathUtil.SmoothThreshold(from, to, x);

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // Sixteen directions round the circle (quicker than working out an angle every time).
        static readonly float[] GradX = new float[16], GradY = new float[16];

        static Noise()
        {
            for (int i = 0; i < 16; i++)
            {
                GradX[i] = (float)System.Math.Cos(i * System.Math.PI / 8.0);
                GradY[i] = (float)System.Math.Sin(i * System.Math.PI / 8.0);
            }
        }

        static float Grad(int x, int y, int px, int py, int seed, float dx, float dy)
        {
            x = (x % px + px) % px;
            y = (y % py + py) % py;
            int g = (int)(Hash(x, y, seed) * 16f) & 15;
            return GradX[g] * dx + GradY[g] * dy;
        }

        /// <summary>Gradient noise at (x, y) in cells, repeating every px by py cells; about 0-1.</summary>
        public static float Periodic(float x, float y, int px, int py, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f), uy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            float a = Grad(ix, iy, px, py, seed, fx, fy), b = Grad(ix + 1, iy, px, py, seed, fx - 1f, fy);
            float c = Grad(ix, iy + 1, px, py, seed, fx, fy - 1f), d = Grad(ix + 1, iy + 1, px, py, seed, fx - 1f, fy - 1f);
            float v = a + (b - a) * ux + ((c + (d - c) * ux) - (a + (b - a) * ux)) * uy;
            return v * 0.7071f + 0.5f;
        }

        /// <summary>Layered tiling noise over u, v (0-1 each): <paramref name="period"/> cells across at the coarsest.</summary>
        public static float Tiling(float u, float v, int periodU, int periodV, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int pu = periodU, pv = periodV;
            for (int o = 0; o < octaves; o++)
            {
                sum += Periodic(u * pu, v * pv, pu, pv, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                pu *= 2;
                pv *= 2;
            }
            return sum / norm;
        }

        /// <summary>Layered noise, not tiling (for small details inside a stamp).</summary>
        public static float Fbm(float x, float y, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Periodic(x * f, y * f, 1 << 20, 1 << 20, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                f *= 2f;
            }
            return sum / norm;
        }
    }
}
