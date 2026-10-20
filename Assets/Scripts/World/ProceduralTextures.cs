using UnityEngine;

/// <summary>
/// Generates textures in code (no image files needed), each made once on first use and shared:
/// - the forest floor: dark, damp soil covered in rotting leaf litter, twigs and the odd stone, plus a
///   matching normal map so the flashlight picks out every bump (tiles seamlessly);
/// - bark (with a normal map) for tree trunks and branches, and pine needles for foliage;
/// - the eyeball of the giant eye in the sky.
/// </summary>
public static class ProceduralTextures
{
    const int Size = 512;

    static Texture2D dirt, dirtNormal, skyEye, bark, barkNormal, needles, wornPath;

    /// <summary>Tiling forest-floor dirt.</summary>
    public static Texture2D Dirt { get { if (dirt == null) MakeDirt(); return dirt; } }
    /// <summary>Normal map matching Dirt.</summary>
    public static Texture2D DirtNormal { get { if (dirtNormal == null) MakeDirt(); return dirtNormal; } }
    /// <summary>Tiling bark: furrowed plates running lengthwise, with faint lichen. Tall (v runs up the trunk).</summary>
    public static Texture2D Bark { get { if (bark == null) MakeBark(); return bark; } }
    /// <summary>Normal map matching Bark.</summary>
    public static Texture2D BarkNormal { get { if (barkNormal == null) MakeBark(); return barkNormal; } }
    /// <summary>Tiling pine needles: dark green streaks running down from the tips.</summary>
    public static Texture2D Needles => needles != null ? needles : (needles = MakeNeedles());
    /// <summary>
    /// A faded strip of trodden dirt: u runs across the path (fading out at both edges), v along it (tiles).
    /// Mostly see-through, so the ground shows through it.
    /// </summary>
    public static Texture2D WornPath => wornPath != null ? wornPath : (wornPath = MakeWornPath());
    /// <summary>A bloodshot red eyeball with a fibrous, glowing iris (no pupil; the shader draws that).</summary>
    public static Texture2D SkyEye => skyEye != null ? skyEye : (skyEye = MakeSkyEye());

    // ---------------------------------------------------------------- Tiling noise

    // Value noise on a lattice that wraps every `period` cells, so the texture tiles seamlessly.
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 144665;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    static float TileNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int ax = Mod(x0, period), bx = Mod(x0 + 1, period);
        int ay = Mod(y0, period), by = Mod(y0 + 1, period);
        float a = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx);
        float b = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    /// <summary>Like TileNoise, but wrapping every px cells across and py cells down (for stretched patterns).</summary>
    static float TileNoise2(float x, float y, int px, int py, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int ax = Mod(x0, px), bx = Mod(x0 + 1, px);
        int ay = Mod(y0, py), by = Mod(y0 + 1, py);
        float a = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx);
        float b = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    /// <summary>Fractal noise over a unit square (u, v in 0..1) that tiles. 0..1.</summary>
    static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        int period = basePeriod;
        for (int o = 0; o < octaves; o++)
        {
            sum += TileNoise(u * period, v * period, period, seed + o * 31) * amp;
            norm += amp;
            amp *= 0.5f;
            period *= 2;
        }
        return sum / norm;
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    // ---------------------------------------------------------------- Scattered details

    // Small things (leaves, twigs, stones) are scattered a few per grid cell. To find what covers a pixel,
    // check its own cell and the eight around it; cells wrap, so details crossing an edge tile correctly.
    struct Detail
    {
        public float x, y, angle, length, width, tint;
    }

    static Detail GetDetail(int gx, int gy, int k, int grid, int seed, float minLength, float maxLength, float widthRatio)
    {
        int hx = Mod(gx, grid), hy = Mod(gy, grid);
        return new Detail
        {
            x = (gx + Hash(hx, hy, seed + k * 7)) / grid,
            y = (gy + Hash(hx, hy, seed + k * 7 + 1)) / grid,
            angle = Hash(hx, hy, seed + k * 7 + 2) * Mathf.PI,
            length = Mathf.Lerp(minLength, maxLength, Hash(hx, hy, seed + k * 7 + 3)),
            width = widthRatio * Mathf.Lerp(0.6f, 1.2f, Hash(hx, hy, seed + k * 7 + 4)),
            tint = Hash(hx, hy, seed + k * 7 + 5),
        };
    }

    /// <summary>Coverage 0..1 of an oriented ellipse (a leaf or stone) at (u, v).</summary>
    static float Ellipse(Detail d, float u, float v, out float along, out float across)
    {
        float dx = u - d.x, dy = v - d.y;
        float c = Mathf.Cos(d.angle), s = Mathf.Sin(d.angle);
        float lx = (dx * c + dy * s) / d.length;
        float ly = (-dx * s + dy * c) / (d.length * d.width);
        along = lx;
        across = ly;
        float e = lx * lx + ly * ly;
        return Mathf.Clamp01((1f - e) * 4f);
    }

    /// <summary>Coverage 0..1 of a thin line segment (a twig) at (u, v).</summary>
    static float Segment(Detail d, float u, float v)
    {
        float dx = u - d.x, dy = v - d.y;
        float c = Mathf.Cos(d.angle), s = Mathf.Sin(d.angle);
        float along = dx * c + dy * s;
        if (Mathf.Abs(along) > d.length) return 0f;
        float across = Mathf.Abs(-dx * s + dy * c);
        float thickness = d.length * d.width * (1f - 0.5f * Mathf.Abs(along) / d.length); // tapers
        return Mathf.Clamp01((1f - across / thickness) * 3f);
    }

    // ---------------------------------------------------------------- Bark & needles

    static void MakeBark()
    {
        const int w = 256, h = 512;
        var height = new float[w * h];
        var albedo = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;

                // Long plates of bark, split by deep vertical cracks, with fine grain.
                float plates = 0.6f * TileNoise2(u * 9f, v * 3f, 9, 3, 21) + 0.4f * TileNoise2(u * 18f, v * 6f, 18, 6, 22);
                float crackNoise = TileNoise2(u * 14f, v * 2f, 14, 2, 23) * 0.7f + TileNoise2(u * 28f, v * 5f, 28, 5, 24) * 0.3f;
                float crack = Mathf.Pow(1f - Mathf.Abs(crackNoise * 2f - 1f), 7f);          // 1 along thin crack lines
                float grain = TileNoise2(u * 64f, v * 32f, 64, 32, 25);
                float hgt = plates * (1f - 0.85f * crack) + grain * 0.1f;
                height[y * w + x] = hgt;

                Color c = Color.Lerp(new Color(0.05f, 0.04f, 0.032f), new Color(0.16f, 0.13f, 0.1f), Mathf.Clamp01(hgt * 1.2f));
                // Patches of pale lichen on the raised plates.
                float lichen = Ramp(0.68f, 0.78f, TileNoise2(u * 6f, v * 10f, 6, 10, 26)) * Mathf.Clamp01(hgt * 1.5f - 0.3f);
                c = Color.Lerp(c, new Color(0.17f, 0.19f, 0.15f), lichen * 0.55f);
                c.a = 1f;
                albedo[y * w + x] = c;
            }
        }

        bark = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
        {
            name = "Bark", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        bark.SetPixels32(albedo);
        bark.Apply(true);

        var normals = new Color32[w * h];
        const float strength = 5f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = height[y * w + Mod(x + 1, w)] - height[y * w + Mod(x - 1, w)];
                float dy = height[Mod(y + 1, h) * w + x] - height[Mod(y - 1, h) * w + x];
                var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                normals[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        barkNormal = new Texture2D(w, h, TextureFormat.RGBA32, true, true)
        {
            name = "Bark Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        barkNormal.SetPixels32(normals);
        barkNormal.Apply(true);
    }

    static Texture2D MakeNeedles()
    {
        const int n = 256;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                // Fine streaks running down the branches, in clumps of lighter and darker green.
                float streaks = TileNoise2(u * 64f, v * 6f, 64, 6, 41) * 0.6f + TileNoise2(u * 128f, v * 12f, 128, 12, 42) * 0.4f;
                float clumps = TileNoise2(u * 8f, v * 4f, 8, 4, 43);
                Color c = Color.Lerp(new Color(0.02f, 0.05f, 0.03f), new Color(0.07f, 0.13f, 0.07f), streaks * (0.5f + 0.7f * clumps));
                c = Color.Lerp(c, new Color(0.06f, 0.08f, 0.04f), Ramp(0.7f, 0.9f, clumps) * 0.4f); // older, browner patches
                c.a = 1f;
                px[y * n + x] = c;
            }
        }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, false)
        {
            name = "Needles", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // ---------------------------------------------------------------- Worn path

    static Texture2D MakeWornPath()
    {
        const int w = 128, h = 256;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1), v = y / (float)h;
                // Ragged edges: the width wanders along the path.
                float wobble = (TileNoise2(0.5f, v * 8f, 4, 8, 51) - 0.5f) * 0.25f;
                float across = Mathf.Abs(u - 0.5f + wobble) * 2f;
                float edge = 1f - Ramp(0.35f, 1f, across);
                float patches = TileNoise2(u * 4f, v * 10f, 4, 10, 52) * 0.6f + TileNoise2(u * 12f, v * 30f, 12, 30, 53) * 0.4f;
                float alpha = edge * Mathf.Lerp(0.15f, 0.75f, patches);

                float grit = TileNoise2(u * 40f, v * 80f, 40, 80, 54);
                Color c = Color.Lerp(new Color(0.1f, 0.085f, 0.07f), new Color(0.17f, 0.15f, 0.12f), patches * 0.6f + grit * 0.4f);
                // The odd pebble pressed into the trodden earth.
                if (grit > 0.82f) c = Color.Lerp(c, new Color(0.22f, 0.21f, 0.2f), 0.6f);
                c.a = alpha;
                px[y * w + x] = c;
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
        {
            name = "Worn Path", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // ---------------------------------------------------------------- Sky eye

    static Texture2D MakeSkyEye()
    {
        var px = new Color32[Size * Size];
        const float irisRadius = 0.4f; // in -1..1 space, matching the shader

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                float px2 = u * 2f - 1f, py2 = v * 2f - 1f;
                float r = Mathf.Sqrt(px2 * px2 + py2 * py2);

                // Sclera: deep crimson, darker toward the corners (it's a ball), threaded with veins.
                float shade = Mathf.Lerp(1f, 0.3f, Ramp(0.45f, 1.1f, r));
                Color color = new Color(0.32f, 0.05f, 0.04f) * shade;
                float ridged = 1f - Mathf.Abs(Fbm(u, v, 5, 4, 13) * 2f - 1f);
                float veins = Mathf.Pow(ridged, 14f) * Ramp(0.42f, 0.8f, r);
                color = Color.Lerp(color, new Color(0.7f, 0.04f, 0.03f), veins);

                if (r < irisRadius + 0.02f)
                {
                    // Iris: fibres radiating from the centre, hot near the pupil, dark at the rim.
                    float angle = Mathf.Atan2(py2, px2) / (Mathf.PI * 2f) + 0.5f;
                    float fibres = TileNoise(angle * 64f, r * 10f, 64, 77) * 0.6f + TileNoise(angle * 160f, r * 3f, 160, 31) * 0.4f;
                    float t = r / irisRadius;
                    Color iris = Color.Lerp(new Color(1f, 0.6f, 0.18f), new Color(0.55f, 0.04f, 0.02f), Mathf.Pow(t, 0.8f));
                    iris *= 0.7f + 0.6f * fibres;
                    iris = Color.Lerp(iris, new Color(1f, 0.85f, 0.45f), Ramp(0.3f, 0f, t) * 0.6f); // glow round the pupil
                    iris *= Mathf.Lerp(1f, 0.12f, Ramp(0.8f, 1f, t));                             // dark limbal ring
                    color = Color.Lerp(iris, color, Ramp(irisRadius - 0.01f, irisRadius + 0.02f, r));
                }

                color.a = 1f;
                px[y * Size + x] = color;
            }
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
        {
            name = "Sky Eye", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // Smooth 0..1 ramp as x goes from edge0 to edge1 (works with edge0 > edge1 too).
    static float Ramp(float edge0, float edge1, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));

    // ---------------------------------------------------------------- Dirt

    static void MakeDirt()
    {
        var height = new float[Size * Size];
        var albedo = new Color32[Size * Size];

        const int leafGrid = 22, twigGrid = 7, stoneGrid = 10;
        var soilDark = new Color(0.028f, 0.022f, 0.017f);
        var soilLight = new Color(0.06f, 0.046f, 0.034f);
        var leafDark = new Color(0.05f, 0.03f, 0.018f);
        var leafLight = new Color(0.095f, 0.06f, 0.03f);
        var twigColor = new Color(0.04f, 0.032f, 0.024f);
        var stoneColor = new Color(0.07f, 0.068f, 0.065f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;

                // Soil: damp darker patches, fine grain and little clods.
                float patches = Fbm(u, v, 3, 3, 11);
                float grain = Fbm(u, v, 32, 3, 47);
                float clods = TileNoise(u * 64f, v * 64f, 64, 91);
                Color color = Color.Lerp(soilDark, soilLight, patches) * (0.75f + 0.45f * grain);
                float h = patches * 0.25f + grain * 0.3f + clods * 0.15f;

                // Half-buried stones.
                int sx = (int)(u * stoneGrid), sy = (int)(v * stoneGrid);
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    var d = GetDetail(sx + ox, sy + oy, 0, stoneGrid, 500, 0.008f, 0.016f, 0.8f);
                    if (d.tint > 0.6f) continue; // only some cells have one
                    float cover = Ellipse(d, u, v, out _, out _);
                    if (cover <= 0f) continue;
                    color = Color.Lerp(color, stoneColor * (0.8f + 0.4f * grain), cover);
                    h = Mathf.Max(h, 0.55f + 0.3f * cover);
                }

                // Twigs.
                int tx = (int)(u * twigGrid), ty = (int)(v * twigGrid);
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                for (int k = 0; k < 2; k++)
                {
                    var d = GetDetail(tx + ox, ty + oy, k, twigGrid, 700, 0.025f, 0.07f, 0.035f);
                    float cover = Segment(d, u, v);
                    if (cover <= 0f) continue;
                    color = Color.Lerp(color, twigColor * (0.7f + 0.6f * d.tint), cover);
                    h = Mathf.Max(h, 0.6f + 0.2f * cover);
                }

                // Rotting leaf litter on top, each leaf with a darker midrib.
                int lx = (int)(u * leafGrid), ly = (int)(v * leafGrid);
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                for (int k = 0; k < 3; k++)
                {
                    var d = GetDetail(lx + ox, ly + oy, k, leafGrid, 900, 0.01f, 0.022f, 0.45f);
                    float cover = Ellipse(d, u, v, out float along, out float across);
                    if (cover <= 0f) continue;
                    float edge = 1f - cover;
                    Color leaf = Color.Lerp(leafDark, leafLight, d.tint) * (0.8f + 0.3f * grain);
                    leaf *= 1f - 0.35f * edge;                              // curled, darker edges
                    float midrib = Mathf.Clamp01(1f - Mathf.Abs(across) * 8f) * Mathf.Clamp01(1f - Mathf.Abs(along));
                    leaf *= 1f - 0.3f * midrib;
                    color = Color.Lerp(color, leaf, cover);
                    h = Mathf.Max(h, 0.5f + 0.15f * cover + 0.05f * d.tint);
                }

                // Crevices between things sit in shadow.
                color *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(h * 1.4f));
                color.a = 1f;

                height[y * Size + x] = h;
                albedo[y * Size + x] = color;
            }
        }

        dirt = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
        {
            name = "Dirt", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        dirt.SetPixels32(albedo);
        dirt.Apply(true);

        // Normal map from the height (wrapping at the edges). Stored as (x, y, z, 1), which Unity's
        // normal unpacking reads correctly on desktop for both pipelines.
        var normals = new Color32[Size * Size];
        const float strength = 6f;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float dx = height[y * Size + Mod(x + 1, Size)] - height[y * Size + Mod(x - 1, Size)];
                float dy = height[Mod(y + 1, Size) * Size + x] - height[Mod(y - 1, Size) * Size + x];
                var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                normals[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        dirtNormal = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true)
        {
            name = "Dirt Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        dirtNormal.SetPixels32(normals);
        dirtNormal.Apply(true);
    }
}
