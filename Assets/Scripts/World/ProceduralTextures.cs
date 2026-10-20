using UnityEngine;

/// <summary>
/// Generates the forest-floor texture in code (no image files needed): dark, damp soil covered in
/// rotting leaf litter, twigs and the odd stone, plus a matching normal map so the flashlight picks out
/// every bump. Made once on first use and shared. The texture tiles seamlessly.
/// </summary>
public static class ProceduralTextures
{
    const int Size = 512;

    static Texture2D dirt, dirtNormal;

    /// <summary>Tiling forest-floor dirt.</summary>
    public static Texture2D Dirt { get { if (dirt == null) MakeDirt(); return dirt; } }
    /// <summary>Normal map matching Dirt.</summary>
    public static Texture2D DirtNormal { get { if (dirtNormal == null) MakeDirt(); return dirtNormal; } }

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
